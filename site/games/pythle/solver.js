(function (root, factory) {
  const solver = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = solver;
  }
  if (root) {
    root.PYTHLE_SOLVER = solver;
  }
})(typeof window !== "undefined" ? window : globalThis, function () {
  "use strict";

  const WORD_LENGTH = 5;

  function countLetters(word) {
    const counts = new Map();
    for (const letter of word) {
      counts.set(letter, (counts.get(letter) || 0) + 1);
    }
    return counts;
  }

  function ensureSet(map, key) {
    if (!map.has(key)) {
      map.set(key, new Set());
    }
    return map.get(key);
  }

  function buildConstraints(history) {
    const fixed = Array(WORD_LENGTH).fill(null);
    const minCounts = new Map();
    const maxCounts = new Map();
    const disallowedPositions = new Map();
    let contradiction = false;

    for (const turn of history) {
      const positivesInTurn = new Map();
      const lettersInTurn = new Set(turn.word);

      turn.feedback.forEach((status, index) => {
        const letter = turn.word[index];
        if (status === "correct") {
          if (fixed[index] && fixed[index] !== letter) {
            contradiction = true;
          }
          fixed[index] = letter;
          positivesInTurn.set(letter, (positivesInTurn.get(letter) || 0) + 1);
        } else if (status === "present") {
          ensureSet(disallowedPositions, letter).add(index);
          positivesInTurn.set(letter, (positivesInTurn.get(letter) || 0) + 1);
        } else {
          ensureSet(disallowedPositions, letter).add(index);
        }
      });

      for (const [letter, count] of positivesInTurn) {
        minCounts.set(letter, Math.max(minCounts.get(letter) || 0, count));
      }

      for (const letter of lettersInTurn) {
        const hasAbsent = turn.feedback.some(
          (status, index) => status === "absent" && turn.word[index] === letter,
        );
        if (!hasAbsent) {
          continue;
        }

        const positiveCount = positivesInTurn.get(letter) || 0;
        const nextMax = positiveCount > 0 ? positiveCount : 0;
        const previousMax = maxCounts.has(letter) ? maxCounts.get(letter) : Infinity;
        maxCounts.set(letter, Math.min(previousMax, nextMax));
      }
    }

    for (const [letter, min] of minCounts) {
      if (maxCounts.has(letter) && maxCounts.get(letter) < min) {
        contradiction = true;
      }
    }

    return { fixed, minCounts, maxCounts, disallowedPositions, contradiction };
  }

  function candidateMatches(entry, constraints) {
    if (constraints.contradiction) {
      return false;
    }

    const word = entry.word;
    for (let index = 0; index < WORD_LENGTH; index += 1) {
      if (constraints.fixed[index] && word[index] !== constraints.fixed[index]) {
        return false;
      }
    }

    for (const [letter, positions] of constraints.disallowedPositions) {
      for (const position of positions) {
        if (word[position] === letter) {
          return false;
        }
      }
    }

    const counts = countLetters(word);
    for (const [letter, min] of constraints.minCounts) {
      if ((counts.get(letter) || 0) < min) {
        return false;
      }
    }

    for (const [letter, max] of constraints.maxCounts) {
      if ((counts.get(letter) || 0) > max) {
        return false;
      }
    }

    return true;
  }

  function buildPoolStats(candidates) {
    const stats = {};
    const total = Math.max(1, candidates.length);

    for (const entry of candidates) {
      const seen = new Set();
      for (let index = 0; index < WORD_LENGTH; index += 1) {
        const letter = entry.word[index];
        if (!stats[letter]) {
          stats[letter] = { usage: 0, positions: Array(WORD_LENGTH).fill(0) };
        }
        stats[letter].positions[index] += 1;
        seen.add(letter);
      }
      for (const letter of seen) {
        stats[letter].usage += 1;
      }
    }

    for (const letter of Object.keys(stats)) {
      stats[letter].usage /= total;
      stats[letter].positions = stats[letter].positions.map((value) => value / total);
    }

    return stats;
  }

  function scoreWord(entry, poolStats, letterStats) {
    const seen = new Set();
    let score = 0;

    for (let index = 0; index < WORD_LENGTH; index += 1) {
      const letter = entry.word[index];
      const global = letterStats[letter] || {
        usage: 0,
        positions: Array(WORD_LENGTH).fill(0),
      };
      const pool = poolStats[letter] || {
        usage: 0,
        positions: Array(WORD_LENGTH).fill(0),
      };

      score += pool.positions[index] * 2.4;
      score += global.positions[index] * 1.2;

      if (!seen.has(letter)) {
        score += pool.usage * 1.8;
        score += global.usage * 0.8;
        seen.add(letter);
      } else {
        score -= 0.18;
      }
    }

    score += Math.log(entry.frequency + 1) * 0.06;
    if (entry.wordleAnswer) {
      score += 0.18;
    }
    if (entry.source === "spreadsheet") {
      score += 0.12;
    }
    if (entry.plural) {
      score -= 0.08;
    }
    return score;
  }

  function scoreLikelyAnswer(entry, poolStats, letterStats) {
    let score = scoreWord(entry, poolStats, letterStats) * 0.5;

    score += Math.log(entry.frequency + 1) * 0.32;
    if (entry.wordleAnswer) {
      score += 3;
    }
    if (entry.source === "spreadsheet") {
      score += 0.9;
    }
    if (entry.source === "expanded" && !entry.wordleAnswer && entry.frequency === 0) {
      score -= 0.25;
    }
    if (entry.plural) {
      score -= 0.65;
    }
    return score;
  }

  function compareRankedWords(a, b) {
    return (
      b.score - a.score
      || Number(b.entry.wordleAnswer) - Number(a.entry.wordleAnswer)
      || b.entry.frequency - a.entry.frequency
      || a.entry.word.localeCompare(b.entry.word)
    );
  }

  function getSuggestions(candidates, limit, letterStats) {
    const poolStats = buildPoolStats(candidates);
    return candidates
      .map((entry) => ({
        entry,
        score: scoreLikelyAnswer(entry, poolStats, letterStats),
      }))
      .sort(compareRankedWords)
      .slice(0, limit);
  }

  function gradeGuess(guess, answer) {
    const feedback = Array(WORD_LENGTH).fill("absent");
    const remaining = countLetters(answer);

    for (let index = 0; index < WORD_LENGTH; index += 1) {
      if (guess[index] === answer[index]) {
        feedback[index] = "correct";
        remaining.set(guess[index], remaining.get(guess[index]) - 1);
      }
    }

    for (let index = 0; index < WORD_LENGTH; index += 1) {
      if (feedback[index] === "correct") {
        continue;
      }
      const letter = guess[index];
      if ((remaining.get(letter) || 0) > 0) {
        feedback[index] = "present";
        remaining.set(letter, remaining.get(letter) - 1);
      }
    }

    return feedback;
  }

  function feedbackKey(feedback) {
    const symbols = { absent: "0", present: "1", correct: "2" };
    return feedback.map((status) => symbols[status]).join("");
  }

  function feedbackCode(guess, answer) {
    let exactMask = 0;
    let claimedMask = 0;

    for (let index = 0; index < WORD_LENGTH; index += 1) {
      if (guess[index] === answer[index]) {
        exactMask |= 1 << index;
      }
    }

    let code = 0;
    for (let index = 0; index < WORD_LENGTH; index += 1) {
      if (exactMask & (1 << index)) {
        code = code * 3 + 2;
        continue;
      }

      let status = 0;
      for (let answerIndex = 0; answerIndex < WORD_LENGTH; answerIndex += 1) {
        const answerBit = 1 << answerIndex;
        if (
          !(exactMask & answerBit)
          && !(claimedMask & answerBit)
          && guess[index] === answer[answerIndex]
        ) {
          claimedMask |= answerBit;
          status = 1;
          break;
        }
      }
      code = code * 3 + status;
    }

    return code;
  }

  function getInformationSuggestions(
    answerCandidates,
    guessCandidates,
    limit,
    letterStats,
    options = {},
  ) {
    if (answerCandidates.length === 1) {
      return [{
        entry: answerCandidates[0],
        score: Infinity,
        entropy: 0,
        expectedRemaining: 1,
      }];
    }

    const priorWeight = Number(options.priorWeight || 0);
    const coverageWeight = Number(options.coverageWeight || 0);
    const singletonWeight = Number(options.singletonWeight || 0);
    const familyCoverageWeight = Number(options.familyCoverageWeight || 0);
    const coverageLetters = options.coverageLetters || new Set();
    const poolStats = buildPoolStats(answerCandidates);
    const answerCount = Math.max(1, answerCandidates.length);
    const ranked = guessCandidates.map((entry) => {
      const buckets = new Uint16Array(243);
      for (const answer of answerCandidates) {
        buckets[feedbackCode(entry.word, answer.word)] += 1;
      }

      let entropy = 0;
      let remainingSquareTotal = 0;
      let worstCaseRemaining = 0;
      let singletonAnswers = 0;
      for (const count of buckets) {
        if (!count) {
          continue;
        }
        const probability = count / answerCount;
        entropy -= probability * Math.log2(probability);
        remainingSquareTotal += count * count;
        worstCaseRemaining = Math.max(worstCaseRemaining, count);
        if (count === 1) {
          singletonAnswers += 1;
        }
      }

      const expectedRemaining = remainingSquareTotal / answerCount;
      const singletonProbability = singletonAnswers / answerCount;
      const prior = scoreLikelyAnswer(entry, poolStats, letterStats);
      const covered = new Set(
        [...entry.word].filter((letter) => coverageLetters.has(letter)),
      );
      const variationCoverage = covered.size;
      let familyCoverageScore = 0;
      if (options.coverageLetterWeights) {
        for (const letter of new Set(entry.word)) {
          familyCoverageScore +=
            options.coverageLetterWeights.get(letter) || 0;
        }
      }
      let familyWorstCase = 0;
      let familyExpectedRemaining = 0;
      if (Array.isArray(options.trapFamilies)) {
        for (const family of options.trapFamilies) {
          const familyBuckets = new Uint16Array(243);
          for (const answer of family) {
            familyBuckets[feedbackCode(entry.word, answer.word)] += 1;
          }
          let familySquares = 0;
          for (const count of familyBuckets) {
            familyWorstCase = Math.max(familyWorstCase, count);
            familySquares += count * count;
          }
          familyExpectedRemaining += familySquares / family.length;
        }
      }
      return {
        entry,
        entropy,
        expectedRemaining,
        worstCaseRemaining,
        singletonProbability,
        prior,
        variationCoverage,
        familyCoverageScore,
        familyWorstCase,
        familyExpectedRemaining,
        score:
          entropy
          + priorWeight * prior
          + coverageWeight * variationCoverage
          + singletonWeight * singletonProbability
          + familyCoverageWeight * familyCoverageScore,
      };
    });

    return ranked
      .sort((a, b) => {
        if (options.optimizeTwoLevelTrap) {
          return (
            b.score - a.score
            || a.worstCaseRemaining - b.worstCaseRemaining
            || a.expectedRemaining - b.expectedRemaining
            || a.entry.word.localeCompare(b.entry.word)
          );
        }
        if (options.optimizeWorstCase) {
          return (
            a.worstCaseRemaining - b.worstCaseRemaining
            || a.expectedRemaining - b.expectedRemaining
            || b.singletonProbability - a.singletonProbability
            || b.score - a.score
            || a.entry.word.localeCompare(b.entry.word)
          );
        }
        return (
          b.score - a.score
          || a.expectedRemaining - b.expectedRemaining
          || b.prior - a.prior
          || a.entry.word.localeCompare(b.entry.word)
        );
      })
      .slice(0, limit);
  }

  function getTrapProfile(answerCandidates, minimumFamilySize = 3) {
    const familiesByWords = new Map();

    for (let mask = 1; mask < 1 << WORD_LENGTH; mask += 1) {
      let fixedPositions = 0;
      for (let index = 0; index < WORD_LENGTH; index += 1) {
        fixedPositions += (mask >> index) & 1;
      }
      if (fixedPositions < 3 || fixedPositions > 4) {
        continue;
      }

      const groups = new Map();
      for (const entry of answerCandidates) {
        let skeleton = "";
        for (let index = 0; index < WORD_LENGTH; index += 1) {
          skeleton += mask & (1 << index) ? entry.word[index] : "_";
        }
        if (!groups.has(skeleton)) {
          groups.set(skeleton, []);
        }
        groups.get(skeleton).push(entry);
      }

      for (const family of groups.values()) {
        if (family.length < minimumFamilySize) {
          continue;
        }
        const key = family
          .map((entry) => entry.word)
          .sort()
          .join(",");
        familiesByWords.set(key, family);
      }
    }

    const families = [...familiesByWords.values()];
    const riskyWords = new Set();
    const variationLetters = new Set();
    const letterWeights = new Map();
    let largestFamily = 0;
    for (const family of families) {
      largestFamily = Math.max(largestFamily, family.length);
      for (const entry of family) {
        riskyWords.add(entry.word);
      }
      for (let index = 0; index < WORD_LENGTH; index += 1) {
        const letters = new Set(family.map((entry) => entry.word[index]));
        if (letters.size > 1) {
          for (const letter of letters) {
            variationLetters.add(letter);
            const increment =
              family.length
              / Math.max(1, answerCandidates.length)
              / letters.size;
            letterWeights.set(
              letter,
              (letterWeights.get(letter) || 0) + increment,
            );
          }
        }
      }
    }

    return {
      families,
      variationLetters,
      letterWeights,
      largestFamily,
      concentration:
        largestFamily / Math.max(1, answerCandidates.length),
      riskProbability:
        riskyWords.size / Math.max(1, answerCandidates.length),
      riskyAnswerCount: riskyWords.size,
    };
  }

  function getVariationLetters(answerCandidates) {
    const letters = new Set();
    for (let index = 0; index < WORD_LENGTH; index += 1) {
      const positionLetters = new Set(
        answerCandidates.map((entry) => entry.word[index]),
      );
      if (positionLetters.size > 1) {
        for (const letter of positionLetters) {
          letters.add(letter);
        }
      }
    }
    return letters;
  }

  function shouldUseMaverickProbe(
    iceManSuggestion,
    probeSuggestion,
    answerCount,
    options = {},
  ) {
    if (!iceManSuggestion || !probeSuggestion) {
      return false;
    }

    const minCandidates = Number(options.minCandidates || 3);
    const maxCandidates = Number(options.maxCandidates || 12);
    const maxExpectedRatio = Number(options.maxExpectedRatio || 0.6);
    const minIceExpected = Number(options.minIceExpected || 1.8);
    const minCoverage = Number(options.minCoverage || 2);
    const maxProbeWorstCase = Number(options.maxProbeWorstCase || 243);
    return (
      answerCount >= minCandidates
      && answerCount <= maxCandidates
      && iceManSuggestion.expectedRemaining >= minIceExpected
      && probeSuggestion.expectedRemaining
        <= iceManSuggestion.expectedRemaining * maxExpectedRatio
      && probeSuggestion.worstCaseRemaining <= maxProbeWorstCase
      && probeSuggestion.variationCoverage >= minCoverage
    );
  }

  function getMaverickSuggestions(
    answerCandidates,
    constrainedGuessCandidates,
    allGuessCandidates,
    history,
    limit,
    letterStats,
    options = {},
  ) {
    const settings = {
      priorWeight: 0.1,
      minCandidates: 3,
      maxCandidates: 300,
      maxExpectedRatio: 0,
      minIceExpected: 1.8,
      minCoverage: 2,
      coverageWeight: 0,
      maxProbeWorstCase: 243,
      maxProbeTurn: 20,
      singletonWeight: 0,
      minSingletonGain: -1,
      optimizeWorstCase: true,
      maxWorstRatio: 0.8,
      maverickFirstMinimax: false,
      optimizeTwoLevelTrap: true,
      minTrapRisk: 0.25,
      minTrapFamilySize: 3,
      maxTrapCandidates: 50,
      minTrapConcentration: 0.3,
      maxTrapTurn: 2,
      familyCoverageWeight: 0.5,
      ...options,
    };
    const iceMan = getInformationSuggestions(
      answerCandidates,
      constrainedGuessCandidates,
      limit,
      letterStats,
      { priorWeight: settings.priorWeight },
    );
    if (!history.length && settings.maverickFirstMinimax) {
      return getInformationSuggestions(
        answerCandidates,
        allGuessCandidates,
        limit,
        letterStats,
        {
          priorWeight: settings.priorWeight,
          optimizeWorstCase: true,
        },
      );
    }
    if (
      !history.length
      || history.length + 1 > settings.maxProbeTurn
      || answerCandidates.length < settings.minCandidates
      || answerCandidates.length > settings.maxCandidates
    ) {
      return iceMan;
    }

    const trapProfile = (
      settings.optimizeTwoLevelTrap
      && history.length + 1 <= settings.maxTrapTurn
    )
      ? getTrapProfile(answerCandidates, settings.minTrapFamilySize)
      : null;
    const trapActive = Boolean(
      trapProfile
      && answerCandidates.length <= settings.maxTrapCandidates
      && trapProfile.riskProbability >= settings.minTrapRisk
      && trapProfile.concentration >= settings.minTrapConcentration
      && trapProfile.largestFamily >= settings.minTrapFamilySize
    );
    const variationLetters = trapActive
      ? trapProfile.variationLetters
      : getVariationLetters(answerCandidates);
    const answerWords = new Set(
      answerCandidates.map((entry) => entry.word),
    );
    const usedWords = new Set(history.map((turn) => turn.word));
    const probeCandidates = allGuessCandidates.filter((entry) => {
      if (answerWords.has(entry.word) || usedWords.has(entry.word)) {
        return false;
      }
      const coverage = new Set(
        [...entry.word].filter((letter) => variationLetters.has(letter)),
      );
      return coverage.size >= settings.minCoverage;
    });
    const probes = getInformationSuggestions(
      answerCandidates,
      probeCandidates,
      limit,
      letterStats,
      {
        priorWeight: 0,
        coverageLetters: variationLetters,
        coverageWeight: settings.coverageWeight,
        singletonWeight: settings.singletonWeight,
        optimizeWorstCase: settings.optimizeWorstCase,
        optimizeTwoLevelTrap: trapActive,
        coverageLetterWeights: trapActive
          ? trapProfile.letterWeights
          : null,
        familyCoverageWeight: settings.familyCoverageWeight,
      },
    );
    const singletonTrigger = (
      settings.minSingletonGain >= 0
      && probes[0]
      && probes[0].singletonProbability
        >= iceMan[0].singletonProbability + settings.minSingletonGain
    );
    const worstCaseTrigger = (
      settings.optimizeWorstCase
      && probes[0]
      && probes[0].worstCaseRemaining
        <= iceMan[0].worstCaseRemaining * settings.maxWorstRatio
    );
    const useProbe = (
      singletonTrigger
      || worstCaseTrigger
      || trapActive
      || shouldUseMaverickProbe(
        iceMan[0],
        probes[0],
        answerCandidates.length,
        settings,
      )
    );
    if (!useProbe) {
      return iceMan;
    }
    return probes.map((probe) => ({
      ...probe,
      maverickProbe: true,
      iceExpectedRemaining: iceMan[0].expectedRemaining,
      trapRisk: trapProfile ? trapProfile.riskProbability : 0,
      trapConcentration: trapProfile ? trapProfile.concentration : 0,
    }));
  }

  return {
    WORD_LENGTH,
    buildConstraints,
    buildPoolStats,
    candidateMatches,
    compareRankedWords,
    countLetters,
    feedbackCode,
    feedbackKey,
    getInformationSuggestions,
    getMaverickSuggestions,
    getSuggestions,
    getTrapProfile,
    getVariationLetters,
    gradeGuess,
    scoreLikelyAnswer,
    scoreWord,
    shouldUseMaverickProbe,
  };
});
