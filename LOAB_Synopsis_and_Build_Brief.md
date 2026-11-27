# Let's Open A Bar! (LOAB)
## Game Synopsis & Build Brief

| | |
|---|---|
| **Document version** | 1.0 (Draft for review) |
| **Date** | November 2026 |
| **Engine** | Unity 6 (LTS) |
| **Platforms** | iOS, Android, Web (WebGL/WebGPU), with PC (Steam) as a stretch target |
| **Genre** | Management / tycoon simulation with light real-time action |
| **Modes** | Solo campaign and sandbox at launch; asynchronous social at launch; co-op and shared-street multiplayer post-launch |
| **Target age rating** | Teen / PEGI 12 (indicative; see Section 18) |

---

## Contents

1. Executive Summary
2. High Concept
3. Target Audience & Platforms
4. Inspirations & What We Take From Each
5. Core Gameplay Loops
6. Game Systems
7. Events System
8. Progression, Modes & Campaign
9. Multiplayer & Social
10. Monetisation
11. Art Direction
12. Audio & Music
13. UX & UI
14. Technical Architecture
15. Content Scope (MVP vs Launch)
16. Production Roadmap
17. Team & Budget Estimate
18. Legal, Ratings & Responsible Design
19. Risks & Mitigations
20. KPIs & Success Metrics
21. Appendices

---

## 1. Executive Summary

Lots of people dream of one day owning their own bar. *Let's Open A Bar!* lets them live that fantasy without the overdraft.

Players start with a tired, empty unit and some savings. They choose the location, knock through walls, pick the vibe, build a drinks menu, hire staff, book a comedian, and open the doors. Then they find out whether anyone turns up.

LOAB combines the cosy, tactile building of *Bakery Story*, the systemic chaos of *Theme Hospital* and *Theme Park*, the "one more year" progression of *Game Dev Story*, and the deep, numbers-driven management of *Football Manager*. It is built in Unity for mobile and web, with sessions designed around a mobile-friendly rhythm: a pausable **Prep phase** for planning, followed by a short, real-time **Service phase** where the night plays out.

The game launches as a strong single-player experience with asynchronous social features (visiting friends' bars, reviews, leaderboards). Co-op "Partnership" play and a competitive-cooperative "High Street" mode follow as major updates. The architecture is built multiplayer-ready from day one.

**The pitch in one line:** *Build the bar you've always talked about, and find out if you'd actually be any good at running it.*

---

## 2. High Concept

### 2.1 Elevator pitch

You've quit the day job. You've got a lease, a leaky roof and a lot of opinions about what makes a good bar. Design every inch, curate the drinks, the music and the crowd, host comedy nights and quiz nights and live gigs, keep the inspectors happy, keep the staff sober, and turn a forgotten unit into the best-loved bar in the city. Then open a second one.

### 2.2 Unique selling points

1. **The fantasy is total.** Location, building style, layout, interior, outdoor space, drinks, food, music, staff, events, branding and pricing are all under the player's control.
2. **Vibe as a system.** Every choice feeds a measurable *Vibe* profile that attracts specific crowds. A dark wood speakeasy with jazz attracts a different crowd than a neon sports bar with pints at £3.50. The game rewards coherence and creativity rather than one correct build.
3. **Events as mini-productions.** Hosting a comedy night, quiz or gig is a planned, booked, promoted and executed event with its own outcomes, stories and reviews.
4. **Prep and Service rhythm.** Calm, pausable planning followed by a tense, short, real-time night. This suits mobile play, and later makes co-op work cleanly.
5. **Stories, not just spreadsheets.** Regulars with names and histories, staff with personalities, disasters and triumphs, all generated from systems and surfaced as memorable moments.
6. **Social without pressure.** Visit friends' bars, leave reviews, swap acts and stock, compete in weekly city leaderboards, and later run a bar together.

### 2.3 Tone

Warm, witty and a little bit British. Affectionate towards bar culture without glamourising excess. Think of a sitcom set in a pub rather than a nightclub advert. Humour comes from situations (the pub quiz team that argues with the quizmaster, the comedian who dies on stage, the dog that somehow got in) rather than from drunkenness.

---

## 3. Target Audience & Platforms

### 3.1 Primary audience

| Segment | Description | What they want from LOAB |
|---|---|---|
| **Cosy builders** | 22–45, mixed gender, play *Bakery Story*, *Stardew Valley*, *The Sims*, *Animal Crossing* | Expression, decoration, gentle progression, short sessions |
| **Management sim fans** | 25–50, play *Football Manager*, *Two Point Hospital*, *Planet Coaster*, *Game Dev Story* | Depth, numbers, optimisation, meaningful decisions |
| **Fantasy owners** | 25–55, have genuinely daydreamed about owning a bar, café or venue | Authenticity, recognisable details, "this is exactly what I'd do" moments |

### 3.2 Secondary audience

- Hospitality workers who enjoy seeing their trade represented with knowing humour.
- Social and casual players drawn in by friends' invitations and shared bar visits.

### 3.3 Platform strategy

| Platform | Role | Notes |
|---|---|---|
| **iOS / Android** | Primary commercial platform | Portrait-friendly menus, landscape for building and service (player choice where possible) |
| **Web (WebGL, WebGPU where supported)** | Discovery, cross-promotion, instant play | Lightweight build, cloud saves shared with mobile, ideal for portals and social sharing |
| **PC (Steam)** | Stretch goal, Year 1–2 | Mouse and keyboard, larger venues, deeper management views, premium price point |

Cross-progression between all platforms via account login is a launch requirement.

---

## 4. Inspirations & What We Take From Each

| Game | What we take | What we avoid |
|---|---|---|
| **Bakery Story** | Tactile decorating, satisfying small loops, visiting friends, collectable décor | Hard energy gates and aggressive timers |
| **Theme Hospital** | Systemic chaos, readable customer AI, humour from emergent problems, room-based building | Fiddly micro-management at scale |
| **Theme Park** | Customer thoughts and needs, pricing experiments, the joy of watching crowds react | Overly abstract economy |
| **Game Dev Story** | Year-on-year growth, staff levelling, "hit or flop" productions (our events), unlockable genres (our bar styles) | Repetitive late-game loop |
| **Football Manager** | Deep staff attributes, scouting and hiring, rich data views for those who want them, emergent narratives | Overwhelming new players; depth is optional in LOAB |
| **Minecraft / 7 Days to Die** (multiplayer) | Persistent shared worlds, drop-in drop-out co-op, players dividing roles naturally | Real-time-only design that clashes with management pacing |

---

## 5. Core Gameplay Loops

### 5.1 The daily loop (moment to moment)

Each in-game day is split into two phases.

**Prep phase (daytime)** is untimed and pausable. The player can:
- Build, decorate and rearrange.
- Order stock and set prices.
- Hire, train and schedule staff.
- Book acts and plan events.
- Launch promotions.
- Review last night's report (takings, reviews, incidents, regulars' feedback).

**Service phase (evening)** is real-time and lasts around 6–10 minutes of play (adjustable in settings; can be sped up or simulated instantly for players who prefer pure management). During service:
- Customers arrive based on Vibe, reputation, promotions, weather, day of the week and events.
- The player reacts to problems: queues, empty kegs, broken glass, spills, a performer running late, a disagreement at the pool table, a surprise coach party.
- The player can step in personally (pull pints, clear tables, calm a situation), redeploy staff, or trigger quick actions such as "happy hour now" or "open the beer garden".
- Event moments play out (the comedian's set, quiz rounds, the band's encore).

**Close** produces a nightly summary: takings, costs, profit, Vibe score, reviews, notable moments, staff morale and stock levels.

**Session design target:** one full day (Prep plus Service plus Close) takes 8–15 minutes, which is ideal for a commute or lunch break.

### 5.2 The weekly loop

- Weekly rhythm affects trade: quiet Mondays, payday Fridays, big Saturdays, Sunday roasts.
- Weekly bills: rent, utilities, wages, supplier invoices.
- Weekly event calendar planning.
- Weekly city leaderboard results (social mode).
- Staff rota and morale checks.

### 5.3 The seasonal loop

Each in-game year has four seasons with real effects:
- **Spring:** beer garden reopens, Easter events.
- **Summer:** outdoor trade peaks, festivals, heatwaves (cold drinks sell, staff tire).
- **Autumn:** students return (in student-heavy locations), Halloween, quiz season.
- **Winter:** Christmas parties, New Year's Eve, January slump ("Dry January" demand for alcohol-free drinks).

Major sports tournaments, local festivals and themed holidays appear as calendar opportunities. These use generic names, not licensed events.

### 5.4 The long-term loop

1. Start with a single small venue.
2. Build reputation and cash.
3. Unlock new bar styles, décor, drinks, event types and locations.
4. Expand the building (second room, upstairs function room, cellar bar, rooftop terrace).
5. Buy a second venue in a new location.
6. Grow into a small group with managers running sites (Football Manager-style delegation).
7. Earn industry awards and city recognition.

---

## 6. Game Systems

### 6.1 Location & property

**Cities and districts.** The launch city has 5–6 districts, each with its own footfall, demographics, rent, competition and licensing rules.

| District archetype | Footfall | Rent | Dominant crowds | Challenges |
|---|---|---|---|---|
| **Old Town** | High | High | Tourists, professionals | Heritage restrictions, noise complaints |
| **Student Quarter** | Seasonal | Low | Students | Low spend per head, term-time swings |
| **Docklands / Warehouse District** | Low to rising | Medium | Creatives, music fans | Needs a reputation to draw people |
| **Suburban High Street** | Medium | Low | Locals, families, older regulars | Loyalty matters more than trends |
| **Business District** | Weekday-heavy | Very high | Office workers | Dead at weekends |
| **Seaside / Riverside** | Weather-dependent | Medium | Day-trippers, families | Seasonal trade |

**Property types.** Players choose from properties with different footprints and personalities:
- Corner pub with a cellar.
- Converted shop unit.
- Railway arch.
- Warehouse floor.
- Victorian townhouse ground floor.
- Rooftop unit (later unlock).
- Riverside boathouse (later unlock).

Each property has fixed features (walls, windows, structural columns, listed features) and condition issues to fix (damp, wiring, plumbing).

**Leasing vs buying.** Leases are cheap to start but rent rises. Buying requires a mortgage but builds equity and allows bigger structural changes.

### 6.2 Building & layout

**Grid-based building** with free rotation for décor items.

Core building tools:
- Walls: build, knock through (with structural checks and costs), doorways, archways.
- Floors: wood, tile, concrete, carpet, sticky carpet (a joke item that appears in old pubs).
- Zones: the player paints zones, and the zone determines function.

| Zone | Function | Key items |
|---|---|---|
| **Bar** | Serves drinks | Counter, taps, back bar, fridges, glass washer, till |
| **Seating** | Customer comfort | Tables, booths, stools, sofas |
| **Stage** | Events | Stage platform, PA, lighting rig |
| **Games** | Dwell time | Pool table, darts, board games, arcade machine, table football |
| **Kitchen** | Food service | Ovens, fryers, prep stations, pass |
| **Toilets** | Essential need | Cubicles, sinks, hand dryers |
| **Cellar / Stores** | Stock capacity | Keg racks, shelving, cold room |
| **Staff area** | Staff morale | Lockers, break table |
| **Outdoor** | Seasonal capacity | Benches, heaters, umbrellas, festoon lights |
| **Function room** | Private hire | Flexible seating, separate bar |

**Flow and pathing.** Customer and staff pathing is simulated. Bottlenecks at the bar, toilets far from seating, or a stage blocking the fire exit create visible problems and lower scores. This is the Theme Hospital lesson: bad layout should be funny and readable, not mysterious.

**Safety and compliance.** Fire exits, capacity limits, accessibility (step-free access, accessible toilets) and kitchen hygiene feed into inspections and licence conditions.

### 6.3 Building styles & exterior

The exterior is the bar's first impression, and it affects passing trade.

- **Façade styles:** traditional pub, industrial, Art Deco, Scandinavian minimal, neon dive, Victorian gin palace, tiki beach shack, alpine lodge.
- **Signage:** custom bar name (with a profanity filter), sign style, hanging sign, painted window lettering, neon.
- **Lighting:** exterior lamps, uplighting, string lights.
- **Planting:** hanging baskets, planters, climbing ivy.
- **Window displays:** affect how inviting the bar looks to passers-by.

### 6.4 Interior design & the Vibe system

**Vibe** is LOAB's central system. Every placed item, colour, light level, music choice, drink and price contributes to a multi-axis Vibe profile.

**Vibe axes (each scored 0–100):**

| Axis | Low end | High end |
|---|---|---|
| **Energy** | Chilled | Lively |
| **Polish** | Rough and ready | Upmarket |
| **Era** | Classic / traditional | Modern / cutting edge |
| **Intimacy** | Big and open | Cosy and intimate |
| **Quirk** | Conventional | Weird and wonderful |

**Coherence score.** Separately, the game measures how coherent the look and feel is. A mix of chesterfields, neon flamingos and plastic chairs scores low unless the player commits to it as a deliberate eclectic style (an unlockable "Maximalist" theme rewards intentional clashing).

**Themes.** Recognised themes give bonuses when enough matching items are present:
- Traditional Pub
- Speakeasy
- Sports Bar
- Craft Beer Taproom
- Cocktail Lounge
- Dive Bar
- Tiki Bar
- Wine Bar
- Live Music Venue
- Gin Palace
- Board Game Café-Bar
- Rooftop Lounge
- Retro Arcade Bar

**Décor catalogue.** Furniture, wall art, lighting, plants, rugs, bar-back displays, ceiling features and novelty items. Each item has Vibe values, a comfort value, a durability rating and a cost. Items wear over time and need repair or replacement.

**Lighting.** Light level and colour temperature are adjustable by zone and time of night. Dimming after 10pm raises Intimacy and Energy differently depending on music.

### 6.5 Outdoor space

Outdoor areas unlock depending on property:
- Front pavement seating (requires a pavement licence).
- Beer garden.
- Courtyard.
- Rooftop terrace.
- Riverside decking.

Features include benches, parasols, heaters, covered smoking/vaping area, outdoor bar, barbecue, garden games (giant Jenga, cornhole), lawn, planting and festoon lighting.

Outdoor space is weather-dependent. The live weather forecast during Prep lets players decide whether to staff and stock the garden. Getting it wrong is costly and funny (an empty garden in a downpour, or a heatwave with the garden closed).

Noise from outdoor areas can trigger neighbour complaints and licence reviews if the bar runs late.

### 6.6 Drinks & menu

**Drink categories:**
- Draught beer and cider (lager, ale, stout, IPA, sour, cider).
- Bottles and cans.
- Wine (red, white, rosé, sparkling, house vs premium).
- Spirits (gin, vodka, rum, whisky, tequila, liqueurs).
- Cocktails (classics plus custom creations).
- Alcohol-free and low-alcohol (alcohol-free beer, mocktails, soft drinks, premium sodas).
- Hot drinks (coffee, tea; enables daytime trade).

**Suppliers.** Fictional breweries, distilleries and wholesalers with different prices, quality, reliability, minimum orders and exclusive deals. Local microbreweries offer unique beers that boost Quirk and loyalty.

**Cocktail Lab.** A light crafting mini-system. Players combine a base spirit, modifiers, mixers, garnish and glassware to create signature cocktails, name them, and price them. Customers rate them. A great signature cocktail becomes a draw in its own right.

**Pricing.** Each drink has cost, price and margin. Customers have price sensitivity by segment. The game shows clear feedback ("Students think your pints are a rip-off").

**Stock management.** Kegs, bottles and perishables have shelf life. Running out during service disappoints customers. Over-ordering ties up cash and wastes stock. Line cleaning is a maintenance task; skip it and quality drops.

**Quality.** Each drink has a quality score influenced by supplier, storage, line cleanliness, staff skill and glassware.

### 6.7 Food

Food is optional and unlocks later.
- Start with **bar snacks** (crisps, nuts, scotch eggs).
- Upgrade to a **kitchen** for a small menu (pub classics, burgers, small plates, pizza, Sunday roast).
- **Street food residency:** hand the kitchen to a third-party vendor for a revenue share (low effort, lower margin).

Food increases dwell time and family trade, and adds hygiene inspections and a chef to manage.

### 6.8 Music & entertainment

**Music sources:**
- Jukebox (customers choose; risk of chaos).
- Curated playlist by genre.
- Resident DJ.
- Live acts (see Events).

**Genres** (original, licence-free in-game music): jazz, soul/funk, indie, rock, pop, electronic, folk, country, hip hop, reggae, lounge, retro (60s–90s pastiches).

Each genre shifts Vibe axes and attracts or repels customer segments. Volume interacts with Intimacy and with neighbour complaints.

**Screens.** TVs and projectors enable sports screenings and are essential for a Sports Bar theme, but lower Intimacy elsewhere.

**Games.** Pool, darts, table football, arcade cabinets, board games, pinball. These increase dwell time and spending for some segments and attract league teams.

### 6.9 Staff

Staff use a Football Manager-style attribute model, simplified on the surface with depth available for players who want it.

**Roles:**

| Role | Key attributes |
|---|---|
| **Bartender** | Speed, Mixology, Charm, Accuracy, Stamina |
| **Floor staff** | Speed, Charm, Awareness, Stamina |
| **Bar back** | Speed, Strength, Reliability |
| **Chef** | Cooking, Speed, Hygiene, Creativity |
| **Security / door staff** | Presence, Judgement, Calm |
| **Cleaner** | Thoroughness, Speed, Reliability |
| **Events manager** | Booking, Negotiation, Promotion |
| **Bar manager** | Leadership, Admin, Judgement (unlocks delegation) |

**Personalities and traits.** Each staff member has 1–3 traits such as *Life and Soul*, *Grumpy but Brilliant*, *Always Late*, *Cocktail Nerd*, *Gossip*, *Calm Under Pressure*, *Secretly a Comedian*. Traits create stories (the bartender who gets discovered at open mic night).

**Hiring.** Post a job ad, receive applicants, interview (light dialogue choices), check references. Better staff demand better wages.

**Training and progression.** Staff gain experience during service and can be sent on courses (cocktail masterclass, cellar management, first aid, conflict resolution).

**Morale.** Influenced by pay, workload, rota fairness, the staff area, tips, management style and team chemistry. Low morale means mistakes, lateness and resignations. High morale means staff who recommend friends and stay for years.

**Rotas.** Simple drag-and-drop weekly rota with an auto-fill option. Fair rotas boost morale; overworking staff causes burnout.

### 6.10 Customers

Customers are individually simulated agents grouped into segments. Each agent has needs, a budget, preferences and a mood.

**Customer segments (launch set):**

| Segment | Likes | Dislikes | Spend |
|---|---|---|---|
| **Students** | Cheap drinks, loud music, quizzes, games | High prices, stuffy vibe | Low |
| **Young professionals** | Cocktails, stylish décor, after-work deals | Sticky tables, slow service | High |
| **Locals / regulars** | Familiarity, real ale, darts, friendly staff | Sudden changes, high prices | Medium |
| **Craft beer enthusiasts** | Rotating taps, local breweries, knowledgeable staff | Generic lager-only bars | Medium–High |
| **Music lovers** | Live gigs, good sound, late opening | Silence, TV sports | Medium |
| **Sports fans** | Screens, pints, atmosphere on match days | No screens, quiet bars | Medium |
| **Date night couples** | Low light, cosy booths, wine and cocktails | Noise, crowds | High |
| **Tourists** | Character, heritage, photogenic spots | Hard-to-find bars | Medium–High |
| **After-work groups** | Large tables, quick service, food | No space for groups | High |
| **Families (daytime)** | Food, gardens, child-friendly areas | Late-night atmosphere | Medium |
| **Creatives** | Quirk, art, events, independent feel | Chain-bar sameness | Medium |

**Customer needs** (Theme Hospital-style): thirst, hunger, comfort, toilet, social, entertainment, plus a "want to leave" threshold driven by mood, budget and time.

**Thought bubbles.** Tapping a customer shows what they're thinking ("The toilets are miles away", "This IPA is incredible", "Why is there a dog on the bar?"). Aggregated thoughts appear in the nightly report.

**Regulars.** Customers who visit repeatedly become named regulars with a portrait, backstory snippet, favourite drink and favourite seat. Regulars bring friends, defend the bar in online reviews, form quiz teams and darts teams, and occasionally create storylines (a first date that becomes a wedding reception booking a year later). Losing regulars hurts.

**Behaviour and incidents.** Customers can get rowdy, argue, cause spills or break glasses. The game depicts this lightly and comically. Responsible service (staff refusing to over-serve, providing water, door policy) reduces incidents and is rewarded.

### 6.11 Promotion & marketing

- **Social media:** post photos of the bar, events and drinks. Better décor and Vibe coherence make better posts. Engagement drives awareness.
- **Flyers and posters:** targeted by district.
- **Happy hours and deals:** time-limited discounts (food deals, cocktail of the week, quiz night offers).
- **Loyalty card:** builds regulars.
- **Collaborations:** partner with local breweries, a nearby restaurant or a theatre for pre-show trade.
- **Press and influencers:** invite a food and drink critic or influencer. High risk, high reward.
- **Word of mouth:** the strongest channel, driven entirely by customer satisfaction.

Awareness is tracked by segment and by district.

### 6.12 Reputation & reviews

- An overall star rating (1–5) on a fictional review site, **BarHopper**.
- Individual written reviews generated from customers' actual experiences ("Lovely little place but waited 20 minutes for a G&T").
- Reputation tags such as *Best Quiz in Town*, *Hidden Gem*, *Great Beer Garden*, *Overpriced*, *Rough on a Saturday*.
- Critic reviews from events like the city's annual "Bar of the Year" awards.

Reviews are a feedback tool first and a score second. Players should be able to read them and know what to fix.

### 6.13 Maintenance & compliance

**Maintenance:**
- Cleanliness (tables, floors, toilets, glassware) degrades during service.
- Equipment wear (glass washer, fridges, beer lines, coffee machine, PA).
- Building condition (roof, plumbing, electrics).
- Breakdowns occur more often on neglected equipment and at the worst possible moment.

**Compliance:**
- **Premises licence** with conditions (opening hours, capacity, noise limits).
- **Inspections:** hygiene (food), fire safety, licensing visits. Results affect reputation and can lead to fines or temporary closure.
- **Neighbour relations:** noise complaints escalate if ignored. Good relations (inviting neighbours to events, controlling outdoor noise) unlock later licences.
- **Late licence applications** for special events.

Compliance is presented as part of the fantasy rather than red tape, with readable checklists and fair warnings.

### 6.14 Finance

- Simple daily view: takings, costs, profit.
- Detailed view for players who want it: P&L by category, margin by drink, wage percentage, cost of goods, cash flow forecast and graphs.
- Loans and investors: bank loans for expansion; an optional "silent investor" who funds growth in exchange for a profit share and occasional unwanted opinions.
- Bills and cash crunches create tension. Running out of cash isn't instant failure; it triggers a recovery arc (emergency loan, selling décor, a crunch "make or break" weekend).

### 6.15 Random events & incidents

A director system (inspired by *RimWorld*'s storyteller) introduces events scaled to the player's situation:

- **Opportunities:** a film crew wants to shoot in the bar; a famous musician drops in; a hen party books the function room; a local brewery offers an exclusive launch.
- **Problems:** the keg delivery is late; a pipe bursts; a heatwave drains stock; a rival opens across the road; a bad review goes viral.
- **Comedy moments:** a dog wanders in and becomes the bar mascot; the quiz machine gains sentience (not really); a customer proposes during the comedy set.
- **Story arcs:** multi-day events such as a staff romance, a regular's retirement party, or a campaign to save the local high street.

Events offer meaningful choices with trade-offs.

### 6.16 Rivals & competition

AI rival bars exist in each district with their own style, prices and reputation. Rivals respond to the player (copying a successful event night, undercutting prices, poaching staff). Players can compete directly or find a niche.

Rival owners are characters with personalities, giving a light narrative thread to the campaign.

---

## 7. Events System

Events are LOAB's equivalent of *Game Dev Story*'s game releases: planned productions that succeed or flop and become memorable highlights.

### 7.1 Event types (launch set)

| Event | Requirements | Draws | Notes |
|---|---|---|---|
| **Pub Quiz** | Quizmaster (staff or booked), PA | Locals, students, groups | Weekly fixture; quiz teams become recurring characters |
| **Comedy Night** | Stage, PA, lighting, booked comedians | Young professionals, creatives, groups | Line-up quality varies; comedians can bomb or go viral |
| **Live Music** | Stage, PA, lighting, booked band | Music lovers, creatives | Genre must suit Vibe; sound quality matters |
| **Open Mic** | Stage, PA | Creatives, students | Cheap to run, unpredictable quality, talent discovery |
| **DJ Night** | DJ booth, sound system, late licence | Students, young professionals | High energy, higher incident risk |
| **Karaoke** | Screen, PA, karaoke system | Groups, students, hen and stag parties | Chaotic fun |
| **Sports Screening** | Screens, sound | Sports fans | Tied to the fictional sports calendar |
| **Tasting Evening** | Supplier partnership | Enthusiasts, couples | Builds drink reputation |
| **Board Game Night** | Games library | Creatives, students | Long dwell time |
| **Private Hire** | Function room or exclusive hire | Bookings | Guaranteed revenue, specific client demands |
| **Seasonal Specials** | Varies | Varies | Halloween, Christmas parties, New Year's Eve, summer festival |
| **Charity Night** | Any event plus charity tie-in | Locals | Reputation and community goodwill |

### 7.2 Event lifecycle

1. **Plan.** Choose event type, date and time. The game shows predicted turnout based on Vibe, reputation and competition that night.
2. **Book.** Browse acts from an agency roster. Each act has fee, quality, reliability, genre and a following. Unknown acts are cheap gambles; established acts are expensive but bring their own crowd.
3. **Set up.** Rearrange the bar for the event (stage area, seating layout, ticketing or free entry, event drinks specials).
4. **Promote.** Allocate promotion budget and channels. Lead time matters.
5. **Run.** The event plays out during Service. The player manages the night (bar rush during the interval, sound issues, a heckler, a performer running late). Highlight moments are shown as short animated vignettes.
6. **Review.** An event report covers attendance, takings, act performance, customer reactions, reviews and any "moments" (a comedian's set going viral, the band asking for a residency).

### 7.3 Example: hosting a comedy night

- **Booking:** pick a headliner and two support acts from the comedy agency. Mix of known names and new talent.
- **Setup:** stage at the end of the room, chairs facing it, dim house lights, spotlight on stage, bar staff briefed to stay quiet during sets.
- **Promotion:** posters in the Student Quarter and Old Town, social media teaser.
- **During the night:** the first support act struggles; the player can trigger "interval early" to rescue the mood. A heckler appears; security or the headliner handles it, depending on choices. The interval rush hits the bar; the player boosts bar staff.
- **Outcomes:** the headliner's set is a hit, a clip "goes viral" on in-game social media and the bar gains the *Best Comedy Night* tag. Next month's comedy night has higher demand and the agency offers better acts.

### 7.4 Residencies and recurring nights

Successful events can become weekly or monthly fixtures. Recurring nights build their own reputation and regular crowd, and the player can grow them into a brand (for example, "Laughing Stock Comedy Club, every Thursday").

---

## 8. Progression, Modes & Campaign

### 8.1 Game modes

| Mode | Description | Launch? |
|---|---|---|
| **Career** | Story-driven campaign from first bar to small bar group | Yes |
| **Sandbox** | Unlimited or custom budget, all unlocked items, adjustable difficulty | Yes |
| **Scenarios** | Specific challenges ("Save the failing village pub", "Make a profit in the Business District in 6 months", "Turn a dive bar into a cocktail lounge") | Yes (6–10 at launch) |
| **Partnership (co-op)** | 2–4 players run one bar together | Post-launch Update 1 |
| **High Street** | Multiple players own bars on a shared street | Post-launch Update 2 |

### 8.2 Career mode structure

**Act 1: "The Dream" (in-game Year 1).** Take over a run-down bar in a cheap district. Tutorialised systems introduced gradually: building, drinks, staff, service, first quiz night. Goal: survive the first year and break even.

**Act 2: "Making a Name" (Years 2–3).** Unlock events, food, outdoor space, cocktails and promotions. Compete with a rival bar owner. Goal: win the district's "Bar of the Year".

**Act 3: "Expansion" (Years 3–5).** Buy a second venue in a different district. Hire a bar manager and learn delegation. Goal: run two profitable venues with different identities.

**Act 4: "The Group" (Years 5+).** Grow to 3–5 venues, open a flagship, and enter the city-wide awards. Endgame challenges include the *Legendary Bar* status and an open-ended continuation.

### 8.3 Unlocks & progression currencies

- **Cash:** the in-game economy, earned from trade.
- **Reputation:** unlocks new districts, properties, suppliers and acts.
- **Owner XP / level:** the player-owner levels up, unlocking skills.
- **Collections:** décor sets and themed collections (complete the "Speakeasy" set for a bonus).

**Owner skill tree (examples):**
- *Host with the Most*: personal service boosts customer mood.
- *Cellar Master*: lower stock wastage.
- *People Person*: staff morale bonus.
- *Wheeler-Dealer*: better supplier deals.
- *Impresario*: better event bookings.
- *Licensing Whisperer*: easier licence applications.

### 8.4 Difficulty

Difficulty presets (Relaxed, Standard, Hardcore) plus custom sliders for customer patience, economy harshness, incident frequency and inspection strictness. Relaxed mode removes bankruptcy entirely for cosy players.

---

## 9. Multiplayer & Social

### 9.1 Design principle

LOAB is **solo-first**. The bar fantasy is personal, and management games depend on player control of time. Multiplayer is layered on in phases, with the architecture prepared from day one so co-op is an extension rather than a rebuild.

The **Prep / Service split** is what makes co-op work. Prep is untimed and shared, so players plan together without one person waiting for another. Service runs at a fixed real-time pace for everyone, so there's no conflict over pausing or speeding up.

### 9.2 Phase 1 (launch): asynchronous social

- **Visit friends' bars:** walk around a snapshot of their bar, see their décor and customers, and leave a review.
- **Gifting:** send stock, décor items or a "round on me" bonus.
- **Act swapping:** recommend acts you've booked to friends; a performer can "tour" friends' bars.
- **Pub Crawl:** visit a chain of friends' bars daily for small rewards.
- **City leaderboards:** weekly categories (Best Quiz, Highest Rated, Most Profitable, Best Beer Garden), with seasonal league tiers.
- **Photo mode and sharing:** share bar photos to real social media with a deep link to visit.
- **Bar codes:** shareable codes or links to visit any public bar.

### 9.3 Phase 2 (Update 1): "Partnership" co-op

- **2–4 players** co-own one persistent bar.
- **Roles** are optional but encouraged: Front of House (floor and customers), Behind the Bar (drinks and stock), Promoter (events and marketing), Money (finance and suppliers). Players can switch freely.
- **Persistence:** the bar lives on the server. Partners drop in and out; when absent, AI staff cover their role.
- **Shared decisions:** large purchases or structural changes require partner approval (vote). A "silent partner" option allows one player to make decisions freely for casual groups.
- **Async contribution:** a partner can do Prep tasks while others are offline (order stock, plan the next event).
- **Session format:** players join a scheduled or ad hoc Service together; the night plays out live.

### 9.4 Phase 3 (Update 2): "High Street"

- 4–8 players each own a bar on the same street or district.
- Shared customer pool: bars compete for footfall but also benefit from the street's overall reputation.
- **Cooperative goals:** street festivals, shared security, "Save Our High Street" campaigns.
- **Competitive elements:** price wars, event clashes, poaching staff (with limits to prevent griefing).
- Seasons with resets and rewards, keeping the mode fresh.

### 9.5 Anti-grief and safety

- Bar names, cocktail names and reviews pass through text filtering and reporting tools.
- No free-form chat with strangers at launch; preset reactions and emoji only in public spaces. Friend chat is optional and moderated.
- Competitive interactions in High Street mode are bounded so no player can wreck another's business.

---

## 10. Monetisation

### 10.1 Model

**Recommended:** free-to-play on mobile and web with fair, cosmetic-focused monetisation, plus a premium version on PC.

Alternative for consideration: premium mobile (one-off price) with paid expansions. This suits the Football Manager and Game Dev Story audience but limits reach. The decision should follow soft-launch testing.

### 10.2 Revenue streams (free-to-play)

| Stream | Description | Fairness guardrails |
|---|---|---|
| **Cosmetic décor packs** | Themed furniture, signage, outfits | Purely visual or sideways; no exclusive power |
| **Season / Bar Pass** | Seasonal track of cosmetic rewards and challenges | Free track always meaningful |
| **Premium currency** | Buy décor, speed up construction | Construction timers short and optional |
| **Expansion packs** | New cities, themes, event types | Substantial content, priced fairly |
| **Supporter pack** | Removes ads, bonus cosmetics | One-off purchase |
| **Optional rewarded ads** | Watch for a small bonus | Never forced; never interrupt Service |

### 10.3 Things we will not do

- No loot boxes or randomised paid rewards (reduces regulatory risk and protects trust).
- No energy systems that block play.
- No pay-to-win in multiplayer.
- No real alcohol brand advertising or sponsorship deals.

---

## 11. Art Direction

### 11.1 Style

- **Stylised 3D**, isometric-leaning camera with zoom and rotation. Warm, chunky, readable, characterful.
- Reference touchpoints: *Two Point Hospital*'s charm, *Overcooked*'s readability, *Animal Crossing*'s cosiness, with warm pub lighting as the signature.
- Lighting is the hero. Bars live or die on atmosphere, so the art budget prioritises real-time lighting, warm glows, neon and evening ambience, scaled sensibly by platform.

### 11.2 Characters

- Stylised, expressive, diverse, readable at small sizes on phones.
- Modular customisation for staff and customers (body types, skin tones, hair, outfits, accessories).
- Expressive animations: celebrating, laughing at the comedian, groaning at a wrong quiz answer, dancing badly.
- The player-owner is a customisable avatar who appears in the bar.

### 11.3 Readability rules

- Customer mood visible at a glance (icons and body language).
- Problems clearly flagged (spills, queues, empty kegs) without cluttering the scene.
- Colour-blind-safe icon and status design.

### 11.4 Platform scaling

| Tier | Target | Lighting | Customers on screen |
|---|---|---|---|
| **Low** (older phones, WebGL) | 30 fps | Baked plus few dynamic lights | ~40 |
| **Medium** (mid-range phones) | 30–60 fps | Mixed | ~80 |
| **High** (flagship phones, PC) | 60 fps | Full dynamic lighting, post-processing | 150+ |

---

## 12. Audio & Music

- **Original soundtrack** across all in-game genres (licence-free and owned by the studio), so the jukebox and playlists work without licensing costs. Consider commissioning several small artists for authenticity.
- **Adaptive ambience:** crowd murmur scales with occupancy and Energy, glasses clinking, laughter, cheers at sports screenings, quiz-round groans.
- **Event audio:** comedy sets use laugh tracks with variable intensity based on performance (no full jokes needed; muffled stage voice plus crowd reaction).
- **UI sound:** satisfying till chimes and pint-pour sounds; tactile build sounds.
- **Mixing:** music is audibly "in the room" (spatialised and filtered) to sell the bar atmosphere.

---

## 13. UX & UI

### 13.1 Principles

- **Touch first,** mouse-friendly second.
- **Depth on demand:** surface simple info by default; let players drill into Football Manager-level data.
- **One-handed menu play** where possible in portrait; building and service favour landscape.
- **Never punish without explaining:** every bad outcome has a visible, understandable cause.

### 13.2 Key screens

- **Bar view** (main 3D view with quick-action bar).
- **Build mode** (catalogue, grid, rotate, zone paint, undo/redo).
- **Drinks & Menu** (list, suppliers, pricing, Cocktail Lab).
- **Staff** (roster, profiles, hiring, rota, training).
- **Events** (calendar, booking agency, event setup).
- **Promotion** (channels, campaigns, social feed).
- **Finance** (summary and detailed reports).
- **City map** (districts, properties, rivals, friends' bars).
- **Nightly report** (the "end of shift" summary; this should feel rewarding to read).
- **Social hub** (friends, visits, leaderboards).

### 13.3 Onboarding

- First session: fewer than 5 minutes to the first customer served.
- Systems introduced over the first in-game week by a mentor character (a semi-retired landlord or landlady who sold you the lease).
- Contextual tips rather than front-loaded tutorials; skippable for experienced players.

### 13.4 Accessibility

- Scalable text and UI.
- Colour-blind modes and shape-coded icons.
- Adjustable Service speed, plus an "auto-manage service" option.
- Subtitles for all voiced lines and captions for key audio cues.
- Reduced motion option.
- Full remappable controls on PC.

---

## 14. Technical Architecture

### 14.1 Engine & versions

- **Unity 6 LTS** (latest LTS at production start), Universal Render Pipeline (URP) for cross-platform scalability.
- **WebGL** build as baseline web target, with **WebGPU** where browser support allows.
- Mobile minimum spec to be validated in prototype (indicatively iOS devices from roughly 2019 onwards; Android 3–4 GB RAM devices).

### 14.2 Core architecture principles

1. **Simulation separated from presentation.** All game logic (customers, staff, economy, events, Vibe) runs in a pure C# simulation layer with no dependency on MonoBehaviours, rendering or frame timing. Presentation observes the simulation and animates it.
   - Benefits: testability, instant-simulate option for Service, server-side validation, and co-op readiness.
2. **Command pattern for all player actions.** Every action ("place item", "set price", "book act", "assign staff") is a serialisable command processed by the simulation.
   - Benefits: undo/redo in build mode, networking for co-op, replays, debugging and anti-cheat.
3. **Fixed-tick simulation.** The simulation runs at a fixed tick rate (for example 10 ticks per in-game minute), with deterministic random seeds per session for reproducibility.
4. **Data-driven content.** Items, drinks, suppliers, acts, events, customer segments and traits are defined in data (ScriptableObjects authored in-editor, exported to JSON for live-ops), so designers can add and balance content without code changes. Remote config allows balancing updates without app releases.

### 14.3 Customer & staff AI

- **Utility AI** for customer decision-making (choose a need to satisfy based on weighted scores).
- **Behaviour trees or state machines** for action execution (walk to bar, queue, order, sit, leave).
- **Pathfinding:** grid-based A* or flow fields on the build grid, with crowd avoidance appropriate to the device tier.
- **Performance:** consider Unity's DOTS/Entities for the simulation of large crowds if profiling demands it; otherwise a lean object-oriented simulation with pooled agents and level-of-detail for animation.
- **LOD for simulation:** off-screen or distant agents simulate at lower fidelity.

### 14.4 Building system

- Grid-based placement with footprint, clearance and adjacency rules.
- Wall system with procedural mesh generation for knock-throughs and doorways.
- Zone painting with validation (for example, the kitchen must connect to the bar or pass).
- Undo/redo via the command stack.

### 14.5 Save system

- Versioned, serialisable game state (JSON or a binary format such as MessagePack).
- Local saves plus cloud saves with conflict resolution (most recent wins, with user prompt on genuine conflicts).
- Save migration support for updates.
- Autosave at the end of each Prep and each Service phase.

### 14.6 Backend & online services

| Function | Candidate solutions |
|---|---|
| Accounts & cross-progression | Unity Gaming Services (Authentication), PlayFab, or Nakama |
| Cloud saves | UGS Cloud Save / PlayFab |
| Leaderboards | UGS Leaderboards / PlayFab |
| Remote config & live-ops | UGS Remote Config / PlayFab |
| Economy & purchases | Server-validated economy; Unity IAP |
| Analytics | UGS Analytics, GameAnalytics, or Firebase |
| Moderation | Text filtering service plus reporting pipeline |
| Bar snapshots (async visits) | Serialised bar layout and state stored server-side |

**Final backend selection** happens in pre-production based on cost modelling, scale and team experience. A single backend provider is preferred to reduce integration overhead.

### 14.7 Networking (post-launch co-op)

- **Candidates:** Photon Fusion or Unity Netcode for GameObjects (plus Unity Relay/Lobby), or a custom authoritative server.
- **Model:** server-authoritative or host-authoritative simulation; clients send commands, receive state snapshots and deltas. The command architecture (14.2) makes this straightforward.
- **Persistence:** partnership bars stored server-side; a lightweight server process simulates offline-tick progress (or resolves it on next load).
- **Requirement for pre-production:** prototype a two-player Service phase over the network, even though co-op ships post-launch, to validate the architecture early.

### 14.8 Performance budgets (indicative)

| Metric | Mobile (mid) | WebGL | PC |
|---|---|---|---|
| Frame rate | 30 fps (60 optional) | 30 fps | 60 fps |
| Initial download | < 200 MB (with on-demand content via Addressables) | < 50 MB initial load | N/A |
| Memory | < 1.5 GB | < 1 GB | < 4 GB |
| Load to playable | < 15 s | < 20 s | < 10 s |

Use **Addressables** for on-demand asset delivery (décor packs, cities, seasonal content).

### 14.9 Tooling

- In-editor content tools for items, drinks, acts and events.
- Balancing spreadsheets synced with data definitions.
- Headless simulation runner for automated balance testing (simulate thousands of in-game nights to check economy curves).
- Debug overlay: agent needs, Vibe breakdown, path heatmaps, economy readouts.
- Automated tests on the simulation layer (unit and regression).
- CI/CD builds for all platforms (Unity Build Automation, GitHub Actions or similar).

### 14.10 Localisation

- All strings externalised from day one (Unity Localization package).
- Launch languages: English, plus French, German, Spanish, Brazilian Portuguese, Italian (FIGS + PT-BR), with Japanese, Korean and Simplified Chinese as fast follows if soft-launch data supports them.
- Bar culture differs by region; consider regional flavour packs (a German beer hall, a Japanese izakaya, an Irish pub, an American dive bar) as expansions.

---

## 15. Content Scope (MVP vs Launch)

### 15.1 Vertical slice / MVP (internal milestone)

Goal: prove the core loop is fun.

- 1 district, 1 property type.
- Building: walls, floors, bar, seating, toilets, 40–60 décor items.
- Vibe system: 5 axes, 3 themes.
- Drinks: 15–20 drinks, 3 suppliers.
- Staff: bartender and floor staff roles, 3 traits.
- Customers: 4 segments.
- Events: Pub Quiz and Comedy Night.
- Full Prep / Service / Close loop.
- Nightly report and basic reviews.
- Placeholder art in the final style direction.

### 15.2 Soft launch

- 3 districts, 4 property types.
- 150+ décor items, 6 themes.
- 50+ drinks, Cocktail Lab, 6 suppliers.
- All staff roles except bar manager.
- 8 customer segments, regulars.
- 6 event types.
- Career Acts 1–2, Sandbox.
- Async social: visits, gifts, leaderboards.
- Monetisation and analytics fully integrated.

### 15.3 Global launch

- Full launch city (5–6 districts), 7 property types.
- 300+ décor items, 13 themes.
- 80+ drinks, food system, outdoor spaces.
- All staff roles and delegation.
- 11 customer segments.
- 12 event types plus seasonal specials.
- Career Acts 1–4, Sandbox, 6–10 Scenarios.
- Full async social suite, Bar Pass, localisation.

---

## 16. Production Roadmap

Indicative timeline for a mid-sized team (see Section 17). Durations depend on final team size and scope decisions.

| Phase | Duration | Key outputs | Gate |
|---|---|---|---|
| **Concept & pre-production** | 3 months | Design pillars, technical prototype (simulation, building, crowd AI), networking spike, art style tests, backend selection | Greenlight: core loop prototype is fun; tech risks understood |
| **Vertical slice** | 4 months | MVP scope (15.1) at near-final quality | Playtests show players want "one more night" |
| **Production (Alpha)** | 6–8 months | Soft launch content, all core systems, monetisation, social | Feature complete |
| **Beta & soft launch** | 3–4 months | Soft launch in test markets (e.g. Canada, Australia, New Zealand, Nordics, Philippines for tech), live balancing | Retention and monetisation targets met (Section 20) |
| **Global launch** | — | Global launch content, marketing beat, platform featuring pitch | Launch |
| **Post-launch Year 1** | Ongoing | Seasonal events, Update 1 (Partnership co-op) at ~4–6 months, Update 2 (High Street) at ~9–12 months, new city expansion, PC version | Live-ops KPIs |

Total to global launch: approximately **16–20 months**.

---

## 17. Team & Budget Estimate

### 17.1 Core team (production peak)

| Discipline | Headcount |
|---|---|
| Game Director / Lead Designer | 1 |
| Systems / Economy Designer | 1–2 |
| Content / Level Designer | 1 |
| Narrative & Writing (part-time or contract) | 0.5–1 |
| Lead Programmer | 1 |
| Gameplay / Simulation Programmers | 2–3 |
| Tools / Backend / Online Programmer | 1–2 |
| UI Programmer | 1 |
| Art Director | 1 |
| 3D Environment / Prop Artists | 2–3 |
| Character Artist / Animator | 2 |
| Tech Artist | 1 |
| UI / UX Designer | 1 |
| Audio (composer and sound design, contract) | 1 |
| QA | 2 (plus external) |
| Producer | 1 |
| Community / Live-ops / Analytics (pre-launch onwards) | 1–2 |
| **Total** | **~22–28** |

### 17.2 Budget (indicative, UK-based studio)

| Item | Range |
|---|---|
| Development to global launch (team costs) | £2.0m – £3.5m |
| External services (outsourced art, audio, localisation, QA) | £300k – £600k |
| Backend, tools and licences (Year 1) | £50k – £150k |
| User acquisition and marketing (launch window) | £500k – £2m+ (scales with free-to-play strategy) |
| Contingency (15–20%) | Included in planning |

A smaller indie version (8–12 people, premium model, narrower scope) is feasible at roughly £600k – £1.2m but would ship later and with reduced content.

These are planning estimates only and should be revalidated against the studio's actual rates and the selected scope.

---

## 18. Legal, Ratings & Responsible Design

### 18.1 Age ratings

The game depicts alcohol service in a business context. Ratings are determined through PEGI, ESRB and IARC (for app stores and web portals) based on the final content. The target is a **teen-appropriate rating** (for example, PEGI 12 / ESRB Teen), which should be confirmed early with a ratings consultant, since depictions and references to alcohol, tone of incidents and any user-generated content all affect the outcome. Store-specific age ratings (Apple App Store, Google Play) follow their own questionnaires and should also be checked.

### 18.2 Responsible design commitments

- **Drunkenness is never the reward.** The game rewards hospitality, atmosphere and good business, not intoxication.
- **Responsible service is a mechanic.** Staff can refuse service, offer water and manage the door, and doing so improves reputation and reduces incidents.
- **Alcohol-free and low-alcohol drinks are a real part of the economy** (and a growing customer segment), not an afterthought.
- **Incidents are light and comic,** never graphic, and never glorify violence.
- **No real-world alcohol brands** at launch. Any future brand partnerships would require legal review against advertising codes, including the UK's CAP/BCAP rules and the Portman Group code, and equivalent rules in other markets.

### 18.3 Monetisation compliance

- No loot boxes or paid random rewards (avoids regulatory issues in markets such as Belgium and the Netherlands and aligns with UK industry principles).
- Clear pricing and purchase confirmation; parental controls respected.
- Compliance with consumer protection, GDPR / UK GDPR, COPPA (if any under-13 exposure) and platform policies.

### 18.4 Intellectual property

- All music, characters, brands, sports teams and events are original or fictional.
- Comedy acts, bands and quizzes use fictional performers and generated or original content.
- Player-created names pass through text moderation.

---

## 19. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **Core loop isn't fun enough** | Medium | High | Prototype early; strict vertical slice gate; frequent playtests with target audience |
| **Simulation depth overwhelms casual players** | High | High | Depth on demand; strong onboarding; Relaxed difficulty; auto-manage options |
| **Mobile performance with large crowds and lighting** | High | Medium | Device tiers; simulation LOD; early profiling on low-end devices; DOTS where justified |
| **WebGL limitations (memory, load times)** | Medium | Medium | Lightweight web build; Addressables; WebGPU where supported |
| **Ratings or store issues around alcohol** | Medium | High | Early ratings consultation; responsible design commitments; content guidelines |
| **Free-to-play monetisation hurts reputation** | Medium | High | Cosmetic-first model; no loot boxes; soft-launch testing; consider premium alternative |
| **Co-op architecture added too late** | Medium | High | Command pattern and simulation separation from day one; networking spike in pre-production |
| **Scope creep ("everything is up for grabs")** | High | High | Strong pillars; MVP and launch scope locked; post-launch roadmap for extras |
| **Crowded management sim market** | Medium | Medium | Clear differentiation (Vibe system, events, bar fantasy); strong art identity; community building early |
| **Backend costs at scale** | Low–Medium | Medium | Cost modelling; async-first social; efficient snapshot storage |

---

## 20. KPIs & Success Metrics

### 20.1 Soft launch targets (free-to-play, indicative)

| Metric | Target |
|---|---|
| Day 1 retention | ≥ 40% |
| Day 7 retention | ≥ 15% |
| Day 30 retention | ≥ 6% |
| Average session length | 10–15 minutes |
| Sessions per day | 2–3 |
| Tutorial completion | ≥ 80% |
| First event hosted | ≥ 60% of players by Day 3 |
| Conversion to payer | 2–4% |
| Store rating | ≥ 4.4 |

### 20.2 Design health metrics

- Theme diversity: percentage of players using each theme (healthy if no single theme dominates beyond ~30%).
- Bankruptcy rate on Standard difficulty (target low but non-zero in Year 1).
- Event success distribution (flops should happen, but not too often).
- Social engagement: percentage of players visiting a friend's bar weekly.
- Time-to-second-venue in Career mode.

---

## 21. Appendices

### Appendix A: Sample day walkthrough

**Thursday, Week 6, Autumn. "The Crooked Anchor", Docklands.**

**Prep.** The forecast says rain, so the player closes the beer garden and moves the heaters inside. Last night's report flagged a long bar queue at 9pm, so they hire a part-time bartender with the *Calm Under Pressure* trait. Tonight is the second Comedy Night; ticket pre-sales are strong after last month's viral clip. The player rearranges seating to face the stage, orders an extra keg of the local IPA that sold out last week, and launches a "two cocktails for £12 before 8pm" deal to fill the early quiet spell.

**Service.** 6pm: a slow start; after-work groups trickle in for the cocktail deal. 7:30pm: the doors fill with the comedy crowd. 8pm: the first act opens; the room is warm. 8:40pm: interval rush; the new bartender handles it well. 9pm: a heckler during the headliner's set; door staff step in and the headliner turns it into a bit, which earns big laughs. 10pm: the glass washer breaks; the player sends the bar back to hand-wash glasses. 11pm: last orders, a happy crowd lingers.

**Close.** Takings £2,940, profit £860. Vibe: Energy 68, Polish 55, Era 60, Intimacy 62, Quirk 71 (coherent "Creative Warehouse" theme). Four new reviews, average 4.5 stars. Regular "Big Dave" brought three friends. Glass washer needs repairing before Saturday. The comedy agency offers a well-known headliner for next month.

### Appendix B: Example data definitions

**Décor item (illustrative):**
```json
{
  "id": "decor_chesterfield_sofa_01",
  "name": "Chesterfield Sofa",
  "category": "seating",
  "footprint": [2, 1],
  "cost": 850,
  "comfort": 9,
  "capacity": 3,
  "durability": 80,
  "vibe": { "energy": -3, "polish": 6, "era": -5, "intimacy": 5, "quirk": 0 },
  "themes": ["speakeasy", "gin_palace", "traditional_pub"],
  "unlock": { "reputation": 20 }
}
```

**Act (illustrative):**
```json
{
  "id": "act_comedian_new_talent_014",
  "type": "comedian",
  "displayName": "Priya Fairweather",
  "fee": 120,
  "quality": 58,
  "reliability": 85,
  "following": 40,
  "styles": ["observational", "storytelling"],
  "traits": ["rising_star"],
  "volatility": 0.35
}
```

**Customer segment (illustrative):**
```json
{
  "id": "segment_young_professionals",
  "spendPerVisit": { "min": 25, "max": 60 },
  "priceSensitivity": 0.4,
  "vibePreference": { "energy": 60, "polish": 75, "era": 70, "intimacy": 55, "quirk": 45 },
  "drinkPreferences": { "cocktails": 0.4, "wine": 0.3, "craft_beer": 0.2, "lager": 0.1 },
  "peakDays": ["thu", "fri"],
  "peakHours": [17, 22],
  "eventAffinity": { "comedy_night": 0.8, "quiz": 0.5, "dj_night": 0.6 }
}
```

### Appendix C: Design pillars (quick reference)

1. **Your bar, your way.** Expression and ownership come first.
2. **Every night tells a story.** Systems create memorable moments.
3. **Easy to open, hard to master.** Depth on demand.
4. **Warm, not wasted.** Celebrate hospitality and community, not excess.
5. **Better together, great alone.** Solo-first, social by design.

### Appendix D: Glossary

| Term | Meaning |
|---|---|
| **Prep phase** | Untimed, pausable daytime planning phase |
| **Service phase** | Real-time evening phase when the bar is open |
| **Vibe** | Five-axis profile of the bar's atmosphere |
| **Coherence** | How consistently the bar's elements fit a style |
| **Theme** | Recognised style that grants bonuses when enough matching items are present |
| **Regular** | A repeat customer who becomes a named character |
| **Residency** | A recurring event night that builds its own reputation |
| **Partnership** | Co-op mode where 2–4 players share one bar |
| **High Street** | Multiplayer mode where players own neighbouring bars |
| **Director** | The system that schedules random events and opportunities |

### Appendix E: Open questions for the next review

1. Free-to-play vs premium on mobile: decide after vertical slice testing and market analysis.
2. Final backend provider selection.
3. DOTS vs object-oriented simulation: decide after crowd performance profiling.
4. Should the player-owner be controllable directly during Service (walk-around) or only through tap commands?
5. Launch city setting: fictional British city, fictional generic Western city, or a choice of regional cities at launch?
6. Depth of food system at launch vs post-launch.
7. Ratings consultation timing and target rating confirmation.

---

*End of document.*
