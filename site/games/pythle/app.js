(function () {
  "use strict";

  const DATA = window.PYTHLE_DATA;
  const SOLVER = window.PYTHLE_SOLVER;
  const WORD_LENGTH = 5;
  const FEEDBACK = ["absent", "present", "correct"];
  const STATUS_LABELS = {
    absent: "absent",
    present: "present but in another position",
    correct: "correct position",
  };
  const SUGGESTION_LIMIT = 24;
  const CANDIDATE_PAGE_SIZE = 60;
  const INFORMATION_PRIOR_WEIGHT = 0.1;
  const MAVERICK_OPTIONS = {
    priorWeight: INFORMATION_PRIOR_WEIGHT,
    maxCandidates: 300,
    maxProbeTurn: 20,
    optimizeWorstCase: true,
    maxWorstRatio: 0.8,
    maxExpectedRatio: 0,
    optimizeTwoLevelTrap: true,
    minTrapRisk: 0.25,
    minTrapFamilySize: 3,
    maxTrapCandidates: 50,
    minTrapConcentration: 0.3,
    maxTrapTurn: 2,
    familyCoverageWeight: 0.5,
  };

  if (!DATA || !Array.isArray(DATA.words) || !DATA.letterStats || !SOLVER) {
    document.body.innerHTML = "<main class=\"app-shell\"><section class=\"panel\"><h1>Pythle</h1><p>Missing data.js. Run python tools/build_data.py to regenerate the word database.</p></section></main>";
    return;
  }

  const {
    buildConstraints,
    candidateMatches,
    getInformationSuggestions: rankInformationSuggestions,
    getMaverickSuggestions: rankMaverickSuggestions,
    getSuggestions: rankSuggestions,
  } = SOLVER;

  const wordEntries = DATA.words.map((entry) => ({
    word: entry.word,
    frequency: Number(entry.frequency || 0),
    plural: Boolean(entry.plural),
    base: entry.base || null,
    source: entry.source || "spreadsheet",
    wordleAnswer: Boolean(entry.wordleAnswer),
    wordlePlayable: Boolean(entry.wordlePlayable),
  }));
  const wordSet = new Set(wordEntries.map((entry) => entry.word));
  const recommendableEntries = wordEntries.filter(
    (entry) => entry.wordlePlayable,
  );
  const letterStats = DATA.letterStats;

  const elements = {
    candidateCount: document.getElementById("candidate-count"),
    guessCount: document.getElementById("guess-count"),
    currentWord: document.getElementById("current-word"),
    letterRow: document.getElementById("letter-row"),
    guessForm: document.getElementById("guess-form"),
    guessInput: document.getElementById("guess-input"),
    resetButton: document.getElementById("reset-button"),
    status: document.getElementById("status-message"),
    suggestions: document.getElementById("suggestions-list"),
    suggestionMethod: document.getElementById("suggestion-method"),
    algorithmButtons: document.querySelectorAll("[data-algorithm]"),
    candidateSearch: document.getElementById("candidate-search"),
    candidateSummary: document.getElementById("candidate-browser-summary"),
    candidateList: document.getElementById("candidate-browser-list"),
    candidateShowMore: document.getElementById("candidate-show-more"),
    history: document.getElementById("history-list"),
    constraintBoard: document.getElementById("constraint-board"),
    positionSource: document.getElementById("position-source"),
    positionStats: document.getElementById("position-stats"),
  };

  const state = {
    history: [],
    activeWord: "",
    feedback: Array(WORD_LENGTH).fill("absent"),
    candidates: wordEntries.slice(),
    candidateQuery: "",
    candidateDisplayLimit: CANDIDATE_PAGE_SIZE,
    algorithm: "ice-man",
  };

  function sanitizeGuess(value) {
    return String(value || "")
      .toLowerCase()
      .replace(/[^a-z]/g, "")
      .slice(0, WORD_LENGTH);
  }

  function resetCandidateBrowser() {
    state.candidateQuery = "";
    state.candidateDisplayLimit = CANDIDATE_PAGE_SIZE;
    elements.candidateSearch.value = "";
  }

  function getSuggestions(candidates, limit) {
    return rankSuggestions(candidates, limit, letterStats);
  }

  function getRecommendedGuesses(candidates, limit) {
    const answerCandidates = candidates.filter((entry) => entry.wordleAnswer);
    if (!answerCandidates.length) {
      return getSuggestions(candidates, limit);
    }

    const guessCandidates = candidates.filter(
      (entry) => (
        entry.wordlePlayable
        && (entry.wordleAnswer || entry.source === "spreadsheet")
      ),
    );
    if (state.algorithm === "maverick") {
      return rankMaverickSuggestions(
        answerCandidates,
        guessCandidates.length ? guessCandidates : answerCandidates,
        recommendableEntries,
        state.history,
        limit,
        letterStats,
        MAVERICK_OPTIONS,
      );
    }
    return rankInformationSuggestions(
      answerCandidates,
      guessCandidates.length ? guessCandidates : answerCandidates,
      limit,
      letterStats,
      { priorWeight: INFORMATION_PRIOR_WEIGHT },
    );
  }

  function getCandidateRows(candidates, limit) {
    return getSuggestions(candidates, limit);
  }

  function setStatus(message, isError) {
    elements.status.textContent = message || "";
    elements.status.classList.toggle("error", Boolean(isError));
  }

  function defaultFeedbackForWord(word, constraints) {
    return Array.from({ length: WORD_LENGTH }, (_, index) => (
      constraints.fixed[index] && word[index] === constraints.fixed[index] ? "correct" : "absent"
    ));
  }

  function setActiveWord(word, constraints = buildConstraints(state.history)) {
    const clean = sanitizeGuess(word);
    state.activeWord = clean.padEnd(WORD_LENGTH, "-").slice(0, WORD_LENGTH);
    state.feedback = defaultFeedbackForWord(state.activeWord, constraints);
    elements.guessInput.value = clean.toUpperCase();
    renderCurrentGuess();
  }

  function cycleFeedback(index) {
    const current = state.feedback[index];
    const next = FEEDBACK[(FEEDBACK.indexOf(current) + 1) % FEEDBACK.length];
    state.feedback[index] = next;
    renderCurrentGuess();
  }

  function renderCurrentGuess() {
    elements.currentWord.textContent = state.activeWord;
    elements.letterRow.replaceChildren();

    for (let index = 0; index < WORD_LENGTH; index += 1) {
      const letter = state.activeWord[index] || "-";
      const status = state.feedback[index];
      const button = document.createElement("button");
      button.type = "button";
      button.className = `letter-tile ${status}`;
      button.textContent = letter;
      button.disabled = letter === "-";
      button.setAttribute("aria-label", `${letter.toUpperCase()} is ${STATUS_LABELS[status]}`);
      button.title = "Click to cycle absent, present, correct";
      button.addEventListener("click", () => cycleFeedback(index));
      elements.letterRow.append(button);
    }
  }

  function renderSuggestions(suggestions) {
    elements.suggestions.replaceChildren();

    if (!suggestions.length) {
      const empty = document.createElement("p");
      empty.className = "empty-state";
      empty.textContent = "No database words match the current feedback.";
      elements.suggestions.append(empty);
      return;
    }

    suggestions.forEach(({ entry, score }, index) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "suggestion-button";
      button.addEventListener("click", () => {
        setActiveWord(entry.word);
        setStatus(`Loaded ${entry.word.toUpperCase()} as the current guess.`);
      });

      const rank = document.createElement("span");
      rank.className = "suggestion-rank";
      rank.textContent = String(index + 1);

      const text = document.createElement("span");
      const word = document.createElement("span");
      word.className = "suggestion-word";
      word.textContent = entry.word;
      const meta = document.createElement("span");
      meta.className = "suggestion-meta";
      meta.textContent = getSuggestionMeta(entry);
      text.append(word, meta);

      const scoreNode = document.createElement("span");
      scoreNode.className = "suggestion-score";
      scoreNode.textContent = score.toFixed(2);

      button.append(rank, text, scoreNode);
      elements.suggestions.append(button);
    });
  }

  function renderCandidateBrowser(candidates) {
    elements.candidateList.replaceChildren();

    const query = state.candidateQuery;
    const filtered = query
      ? candidates.filter((entry) => entry.word.includes(query))
      : candidates;
    const visible = getCandidateRows(filtered, state.candidateDisplayLimit);
    const shown = Math.min(visible.length, filtered.length);

    elements.candidateSummary.textContent = query
      ? `${shown.toLocaleString()} of ${filtered.length.toLocaleString()} matches`
      : `${shown.toLocaleString()} of ${filtered.length.toLocaleString()} shown`;

    elements.candidateShowMore.hidden = shown >= filtered.length;

    if (!filtered.length) {
      const empty = document.createElement("p");
      empty.className = "empty-state";
      if (query && wordSet.has(query)) {
        empty.textContent = `${query.toUpperCase()} is in the database but excluded by the current feedback.`;
      } else if (query) {
        empty.textContent = `No current candidates match ${query.toUpperCase()}.`;
      } else {
        empty.textContent = "No current candidates remain.";
      }
      elements.candidateList.append(empty);
      return;
    }

    visible.forEach(({ entry }, index) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "candidate-button";
      button.addEventListener("click", () => {
        setActiveWord(entry.word);
        setStatus(`Loaded ${entry.word.toUpperCase()} as the current guess.`);
      });

      const rank = document.createElement("span");
      rank.className = "suggestion-rank";
      rank.textContent = String(index + 1);

      const text = document.createElement("span");
      const word = document.createElement("span");
      word.className = "suggestion-word";
      word.textContent = entry.word;
      const meta = document.createElement("span");
      meta.className = "suggestion-meta";
      meta.textContent = getSuggestionMeta(entry);
      text.append(word, meta);

      button.append(rank, text);
      elements.candidateList.append(button);
    });
  }

  function getSuggestionMeta(entry) {
    const parts = [];
    if (entry.wordleAnswer) {
      parts.push("Wordle answer");
    }
    if (entry.source === "spreadsheet") {
      parts.push(`freq ${entry.frequency}`);
    } else if (entry.wordlePlayable && !entry.wordleAnswer) {
      parts.push("Wordle playable");
    } else if (!entry.wordleAnswer) {
      parts.push("expanded list");
    }
    if (entry.plural) {
      parts.push("plural");
    }
    return parts.join(" - ");
  }

  function renderHistory() {
    elements.history.replaceChildren();

    if (!state.history.length) {
      const empty = document.createElement("p");
      empty.className = "empty-state";
      empty.textContent = "Submit feedback after each Wordle guess.";
      elements.history.append(empty);
      return;
    }

    state.history.forEach((turn, index) => {
      const row = document.createElement("div");
      row.className = "history-row";

      const number = document.createElement("strong");
      number.textContent = `#${index + 1}`;

      const tiles = document.createElement("div");
      tiles.className = "history-word";
      turn.word.split("").forEach((letter, letterIndex) => {
        const tile = document.createElement("span");
        tile.className = `history-tile ${turn.feedback[letterIndex]}`;
        tile.textContent = letter;
        tiles.append(tile);
      });

      const meta = document.createElement("span");
      meta.className = "history-meta";
      meta.textContent = turn.feedback.every((status) => status === "correct") ? "solved" : "applied";

      row.append(number, tiles, meta);
      elements.history.append(row);
    });
  }

  function mapToSortedArray(map) {
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
  }

  function renderConstraints(constraints) {
    elements.constraintBoard.replaceChildren();

    const slots = document.createElement("div");
    slots.className = "fixed-slots";
    constraints.fixed.forEach((letter, index) => {
      const slot = document.createElement("div");
      slot.className = letter ? "slot filled" : "slot";
      slot.textContent = letter || String(index + 1);
      slots.append(slot);
    });

    const groups = document.createElement("div");
    groups.className = "constraint-groups";
    groups.append(
      makeConstraintGroup("Required", mapToSortedArray(constraints.minCounts), "present", ([letter, count]) => `${letter} x${count}`),
      makeConstraintGroup("Excluded", mapToSortedArray(constraints.maxCounts).filter(([, count]) => count === 0), "absent", ([letter]) => letter),
      makeConstraintGroup("Wrong spots", mapToSortedArray(constraints.disallowedPositions), "correct", ([letter, positions]) => `${letter}: ${[...positions].map((position) => position + 1).join(",")}`)
    );

    elements.constraintBoard.append(slots, groups);
  }

  function getPositionStats(candidates, limit) {
    const pool = candidates.length ? candidates : wordEntries;
    const total = Math.max(1, pool.length);
    const countsByPosition = Array.from({ length: WORD_LENGTH }, () => new Map());

    for (const entry of pool) {
      for (let index = 0; index < WORD_LENGTH; index += 1) {
        const letter = entry.word[index];
        const counts = countsByPosition[index];
        counts.set(letter, (counts.get(letter) || 0) + 1);
      }
    }

    return {
      total,
      fallback: candidates.length === 0,
      positions: countsByPosition.map((counts) => (
        [...counts.entries()]
          .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
          .slice(0, limit)
          .map(([letter, count]) => ({ letter, count, rate: count / total }))
      )),
    };
  }

  function renderPositionStats(stats) {
    elements.positionStats.replaceChildren();
    elements.positionSource.textContent = stats.fallback ? "Full database fallback" : "Current candidates";

    stats.positions.forEach((letters, index) => {
      const column = document.createElement("article");
      column.className = "position-column";

      const heading = document.createElement("h3");
      heading.textContent = `Position ${index + 1}`;

      const rows = document.createElement("div");
      rows.className = "position-bars";

      letters.forEach(({ letter, count, rate }) => {
        const row = document.createElement("div");
        row.className = "position-row";
        row.title = `${letter.toUpperCase()}: ${count.toLocaleString()} of ${stats.total.toLocaleString()}`;

        const letterNode = document.createElement("span");
        letterNode.className = "position-letter";
        letterNode.textContent = letter;

        const bar = document.createElement("span");
        bar.className = "position-bar";
        const fill = document.createElement("span");
        fill.style.width = `${Math.round(rate * 100)}%`;
        bar.append(fill);

        const rateNode = document.createElement("span");
        rateNode.className = "position-rate";
        rateNode.textContent = `${Math.round(rate * 100)}%`;

        row.append(letterNode, bar, rateNode);
        rows.append(row);
      });

      column.append(heading, rows);
      elements.positionStats.append(column);
    });
  }

  function makeConstraintGroup(title, items, variant, formatter) {
    const group = document.createElement("div");
    group.className = "constraint-group";

    const heading = document.createElement("h3");
    heading.textContent = title;
    const chips = document.createElement("div");
    chips.className = "chips";

    if (!items.length) {
      const empty = document.createElement("span");
      empty.className = "chip";
      empty.textContent = "none";
      chips.append(empty);
    } else {
      for (const item of items) {
        const chip = document.createElement("span");
        chip.className = `chip ${variant}`;
        chip.textContent = formatter(item);
        chips.append(chip);
      }
    }

    group.append(heading, chips);
    return group;
  }

  function recompute() {
    const constraints = buildConstraints(state.history);
    state.candidates = wordEntries.filter((entry) => candidateMatches(entry, constraints));
    const suggestions = getRecommendedGuesses(
      state.candidates,
      SUGGESTION_LIMIT,
    );

    elements.candidateCount.textContent = state.candidates.length.toLocaleString();
    elements.guessCount.textContent = state.history.length.toLocaleString();
    renderSuggestions(suggestions);
    const isProbe = Boolean(suggestions[0] && suggestions[0].maverickProbe);
    elements.suggestionMethod.textContent = state.algorithm === "maverick"
      ? (isProbe ? "Maverick probe" : "Maverick")
      : "Ice-Man";
    renderCandidateBrowser(state.candidates);
    renderHistory();
    renderConstraints(constraints);
    renderPositionStats(getPositionStats(state.candidates, 5));

    return { constraints, suggestions };
  }

  function submitFeedback() {
    const guess = sanitizeGuess(elements.guessInput.value || state.activeWord);
    if (guess.length !== WORD_LENGTH) {
      setStatus("Enter a 5-letter guess before submitting feedback.", true);
      return;
    }

    const feedback = state.feedback.slice();
    state.history.push({ word: guess, feedback });
    resetCandidateBrowser();
    const solved = feedback.every((status) => status === "correct");
    const { constraints, suggestions } = recompute();

    if (solved) {
      setActiveWord(guess);
      state.feedback = Array(WORD_LENGTH).fill("correct");
      renderCurrentGuess();
      setStatus(`${guess.toUpperCase()} is marked solved.`);
      return;
    }

    if (constraints.contradiction) {
      setStatus("The submitted feedback conflicts with earlier guesses.", true);
      return;
    }

    if (!suggestions.length) {
      setStatus("No matching words remain. Check the feedback colors, especially repeated letters.", true);
    } else {
      setActiveWord(suggestions[0].entry.word, constraints);
      const customNote = wordSet.has(guess) ? "" : `${guess.toUpperCase()} is not in the full database. `;
      setStatus(`${customNote}Next suggested guess: ${suggestions[0].entry.word.toUpperCase()}.`);
    }
  }

  elements.guessInput.addEventListener("input", (event) => {
    const clean = sanitizeGuess(event.target.value);
    event.target.value = clean.toUpperCase();
    if (clean.length === WORD_LENGTH) {
      state.activeWord = clean;
      state.feedback = defaultFeedbackForWord(clean, buildConstraints(state.history));
      renderCurrentGuess();
    }
  });

  elements.candidateSearch.addEventListener("input", (event) => {
    const clean = sanitizeGuess(event.target.value);
    event.target.value = clean.toUpperCase();
    state.candidateQuery = clean;
    state.candidateDisplayLimit = CANDIDATE_PAGE_SIZE;
    renderCandidateBrowser(state.candidates);
  });

  elements.candidateShowMore.addEventListener("click", () => {
    state.candidateDisplayLimit += CANDIDATE_PAGE_SIZE;
    renderCandidateBrowser(state.candidates);
  });

  elements.algorithmButtons.forEach((button) => {
    button.addEventListener("click", () => {
      state.algorithm = button.dataset.algorithm;
      elements.algorithmButtons.forEach((option) => {
        const active = option.dataset.algorithm === state.algorithm;
        option.classList.toggle("active", active);
        option.setAttribute("aria-pressed", String(active));
      });
      const { constraints, suggestions } = recompute();
      setActiveWord(
        suggestions[0] ? suggestions[0].entry.word : "",
        constraints,
      );
      setStatus(
        `${state.algorithm === "maverick" ? "Maverick" : "Ice-Man"} selected.`,
      );
    });
  });

  elements.guessForm.addEventListener("submit", (event) => {
    event.preventDefault();
    submitFeedback();
  });

  elements.resetButton.addEventListener("click", () => {
    state.history = [];
    resetCandidateBrowser();
    const { suggestions } = recompute();
    setActiveWord(suggestions[0] ? suggestions[0].entry.word : "");
    setStatus("Solver reset.");
  });

  const initial = recompute().suggestions[0];
  setActiveWord(initial ? initial.entry.word : "");
  setStatus("Click each tile once for yellow, twice for green, then submit.");
})();
