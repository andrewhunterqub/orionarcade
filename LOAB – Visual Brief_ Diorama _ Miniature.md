# Let's Own A Bar! — Visual Brief: Diorama / Miniature

|  |  |
| --- | --- |
| **Document** | Visual brief, art direction |
| **Style** | Diorama / miniature, built on chunky stylised 3D |
| **Version** | 1.0 (draft for art team review) |
| **Date** | November 2026 |
| **Engine** | Unity 6 LTS, Universal Render Pipeline |
| **Platforms** | iOS, Android, Web (WebGL/WebGPU), PC later |
| **Companion documents** | LOAB Synopsis & Build Brief; graphic styles mood board |

---

## 1. Summary

Every bar in LOAB is presented as a handmade scale model: a cutaway room sitting on a plinth, lit like a tiny stage set, and photographed with a gentle tilt-shift blur that makes it feel small, precious and real. Inside the model, everything is built in a chunky, friendly, stylised 3D language that reads clearly on a phone screen.

The player isn't just running a bar. They are building the model of the bar they've always imagined, and the presentation should make them want to show it off.

**In one line:** *A dream bar you could hold in your hands.*

---

## 2. Why this style

The diorama approach does four jobs for LOAB at once.

It turns ownership into something physical. A bar presented as a model feels crafted and collectable, which strengthens the core fantasy of "this is mine".

It solves the camera. Management games need an elevated, readable view of a whole space. A cutaway model makes that elevated view feel natural rather than like a floating security camera.

It's built for sharing. Diorama images are instantly attractive in screenshots, store pages and social posts, which supports the asynchronous social features (visiting friends' bars, photo mode, leaderboards).

It scales across devices. The underlying chunky stylised 3D is cheap enough for mobile and web, and the miniature effects can be reduced or faked on weaker hardware without losing the identity.

---

## 3. Visual pillars

Every asset, shader and screen should be checked against these five pillars.

**Handmade, not manufactured.** Surfaces look painted, sanded, glued and assembled by someone who cared. Slight imperfections are welcome. Sterile, perfectly clean CG is not.

**Tiny and precious.** The camera, depth of field, lighting and materials all work together to suggest the bar is roughly the size of a shoebox. Players should feel a little protective of it.

**Warm light is the hero.** Bars live or die on atmosphere. Pendant lamps, candles, neon signs and the glow from the bar back are the most important visual elements in the game, and the lighting budget is prioritised accordingly.

**Readable first.** Customers, staff, problems and opportunities must be legible at a glance on a 6-inch phone. When style and readability conflict, readability wins.

**Honest cutaway.** We are openly showing a model. Cut wall edges, the plinth and the studio background are part of the look and are never hidden or apologised for.

---

## 4. Camera & framing

### 4.1 Camera model

The camera uses perspective projection with a narrow field of view (around 20–25 degrees) placed at a distance. This flattens perspective slightly and creates the look of a macro photograph of a model, which is the foundation of the miniature effect. A wide field of view breaks the illusion and must be avoided.

The default pitch is 40 degrees above horizontal, with a permitted range of 30–60 degrees. Low angles give dramatic event shots; high angles help with building and layout.

Rotation is limited. In normal play the camera snaps between four corner views at 45-degree offsets, with a smooth 0.35-second ease between them. Free orbit is available only in photo mode. This protects the cutaway logic and keeps lighting predictable.

Zoom ranges from a full-plinth view (the entire model and its frame visible) down to a "table-top" view where two or three customers fill the screen. Depth-of-field strength scales with zoom (see Section 5).

### 4.2 Cutaway behaviour

Walls facing the camera are automatically lowered to a "cut height" of roughly knee height for characters, so the interior is always visible. When the camera rotates, the walls that face it lower, and the others rise back to full height, over 0.25 seconds.

Every cut wall shows a visible cut edge with a light, slightly textured cap, like foam board or MDF. The cut edge colour is consistent across all themes (see Section 7) and forms part of the frame.

The roof is always removed during play. Upper floors (function room, rooftop terrace) are viewed one level at a time, with the level above lifted away and the level below shown as a darkened, simplified base.

### 4.3 Composition

The plinth is always slightly visible at the bottom of the frame at default zoom, anchoring the model. The studio background fills the space around the model and never competes with it. Key interactive areas (the bar counter, stage and entrance) should fall within the central two thirds of the default view.

---

## 5. Tilt-shift & depth of field

Tilt-shift blur is the single strongest signal that the bar is a miniature. It must be beautiful, but never get in the way of play.

### 5.1 Rules

The focal plane always sits on the area the player is looking at. By default this is the centre of the screen, but it follows taps and selections smoothly over 0.3 seconds.

Blur increases towards the top and bottom of the screen, never the sides, imitating a tilted lens. A soft band in the middle third of the screen stays fully sharp at all zoom levels.

Blur strength scales with zoom. It is subtle at full-plinth view and stronger when zoomed in close, as a real macro lens would behave.

Nothing that requires player attention is ever blurred. Problem icons, customer thought bubbles, staff alerts and UI markers render after the blur pass and stay crisp.

In build mode, blur is reduced to around 30% of its normal strength so players can judge placement precisely. In photo mode, it is fully adjustable.

### 5.2 Strength guide

| Context | Blur strength | Sharp band |
| --- | --- | --- |
| Full-plinth view | Low | Middle 60% of screen |
| Default play view | Medium | Middle 40% |
| Close "table-top" view | High | Middle 30% |
| Build mode | Low, fixed | Middle 70% |
| Service phase event moments | High, with focus on performer or incident | Follows subject |
| Photo mode | Player-controlled | Player-controlled |

---

## 6. Scale & proportion

The game uses a one-metre build grid. Characters are approximately 1.6 grid units tall, with a head-to-body ratio of roughly 1:3.5, which is chunkier than real life but not chibi.

Furniture and props are slightly enlarged relative to characters (around 110% of real-world proportion) so they read clearly from the default camera. Small items such as glasses, bottles and taps are enlarged further (around 130%) because they carry important information about service and stock.

Wall height is 3 grid units for standard rooms, with a cut height of around 0.8 units when lowered. The plinth extends 1 grid unit beyond the building footprint on all sides, giving room for pavement, planters and outdoor seating to sit on the model.

---

## 7. Colour

### 7.1 Core palette

These colours come from the approved mood board mock-up and form the default "house" palette for a traditional bar.

| Role | Colour | Hex |
| --- | --- | --- |
| Warm wall | Peach plaster | `#F2B38A` |
| Wall panelling | Terracotta | `#D98A63` |
| Floor | Honey oak | `#B9805A` |
| Bar counter | Polished mahogany | `#A8573A` |
| Signage | Bottle green | `#3D8A7A` |
| Signage lettering | Cream | `#FFF1C9` |
| Accent (upholstery) | Coral red | `#FF6B6B` |
| Accent (clothing) | Cornflower blue | `#5A9BE0` |
| Accent (warmth) | Amber | `#FFB347` |
| Planting | Leaf green | `#6CC070` |
| Lamp glow | Warm light | `#FFD27A` |

### 7.2 The frame palette

These colours never change with the bar's theme. They form a consistent frame around every player's creation, which keeps the game recognisable and ties all player bars together visually.

| Role | Hex | Notes |
| --- | --- | --- |
| Cut edge / foam board | `#FBE3CF` | Slightly warm white with fine grain |
| Plinth (starter) | `#4A3326` | Dark stained wood; upgrades later (Section 9) |
| Studio background, top | `#D9E3EA` | Cool grey-blue, daytime |
| Studio background, bottom | `#9FB2C0` | Cool grey-blue, daytime |
| Studio background, night | `#2A3440` to `#141A21` | Used during Service phase |

The cool studio background deliberately contrasts with the warm interiors, so the bar always feels like the warmest, most inviting thing on screen.

### 7.3 Colour rules

Interiors follow a rough 60/30/10 split: a dominant surface colour (walls and floors), a secondary material colour (furniture and bar), and a small amount of saturated accent (upholstery, signage, clothing, neon).

Customers and staff should always be more saturated than the surfaces behind them, so people pop out of the scene. Background surfaces should stay in the mid-to-low saturation range.

Each bar theme (speakeasy, sports bar, tiki and so on) has its own interior palette, but every theme palette must be tested against the frame palette and the studio background for contrast and harmony.

---

## 8. Materials & surfaces

The goal is "miniature materials": surfaces that look like the materials a skilled model-maker would use to represent real ones.

All materials are slightly matte, with soft, broad highlights. High-gloss reflections are reserved for a few hero materials (bar brass, glassware, polished counter tops) to keep them special.

Every hard edge has a small bevel that catches light. This is one of the most important rules in the style, because bevels make objects feel physical and handmade, and they read well at small sizes.

Surfaces carry a subtle, low-contrast noise or grain (sanded wood, painted card, plaster) applied through a shared detail texture. This replaces photographic texture detail.

| Real material | Miniature treatment |
| --- | --- |
| Wood | Painted and lightly sanded, simple grain lines, slightly visible brush direction |
| Brick | Embossed card, soft rounded bricks, mortar slightly recessed |
| Plaster | Smooth painted card with very fine grain |
| Metal (brass, chrome) | Smooth with a soft but bright specular; slightly exaggerated warmth on brass |
| Glass | Clear resin with a soft rim highlight and coloured liquid inside |
| Drinks | Coloured resin; beer has a cream foam "cap" that reads clearly from above |
| Fabric and upholstery | Felt or flocked texture, slightly fuzzy at the edges |
| Grass and planting | Model flock and foam clumps; trees are rounded foam forms |
| Water (riverside venues) | Gloss resin with gentle animated ripples |
| Neon | Bright emissive tubes with bloom, slightly chunky like model wire |

Photographic textures, realistic dirt and heavy wear are out of scope. Wear is shown through simplified cues (chipped paint, a faded patch, a wobble), not grime.

---

## 9. The frame: plinth, cut edges & studio

The frame is a defining feature of LOAB's identity and should be treated as part of the game, not decoration.

### 9.1 The plinth

The plinth is the base the model sits on. It carries a small engraved or printed plaque at the front with the bar's name, which appears in screenshots and visits.

Plinths upgrade with progression, giving players a visible status symbol that doesn't interfere with their interior design.

| Tier | Plinth | Plaque |
| --- | --- | --- |
| Starter | Plain cardboard base with visible corrugation | Handwritten label |
| Established | Painted MDF | Printed label |
| Respected | Stained oak with routed edge | Engraved card |
| Renowned | Walnut with inlay | Brass plaque |
| Legendary | Marble or polished hardwood with display lighting | Engraved brass with award ribbon |

Cosmetic plinths (for example, seasonal or themed ones) are a natural fit for fair, purely cosmetic monetisation.

### 9.2 Unbuilt and locked areas

Areas the player hasn't bought or built yet are shown as unpainted white card or foam blocks, like an architect's massing model. Neighbouring buildings on the street are also shown this way, with simple window cut-outs and no colour.

This gives LOAB a clear visual language: colour means "yours". As the player expands into a second room, a cellar bar or a rooftop, the white card is "painted" in with a short, satisfying transition.

### 9.3 Studio background

The background is a soft, seamless studio backdrop with a gentle gradient. It shifts between a cool daytime tone during the Prep phase and a dark, moody tone during the Service phase, so the warm interior becomes the light source in the scene at night.

A very soft contact shadow sits under the plinth. Nothing else appears in the background.

---

## 10. Lighting

Lighting carries the mood of every bar and must be planned as carefully as the layout.

### 10.1 Prep phase (daytime)

Light is cooler and brighter, coming mainly from windows and the open "sky" above the model. Interiors look clean and practical. This is the "working light" for building, planning and arranging, and it should be even enough to make placement easy.

### 10.2 Service phase (evening)

The studio background darkens and the bar becomes a glowing object. Practical lights (pendants, wall lamps, candles, back-bar lighting, neon signs and stage lights) become the main light sources. Light pools on tables and the bar top, and soft shadows fall towards the edges of the room.

The transition from Prep to Service is a key moment. Over about three seconds, the background darkens, lights switch on one by one with a gentle bloom, and the first customers appear at the door. This should feel like a theatre curtain rising.

### 10.3 Lighting rules

Every lamp the player places must visibly affect the room, at least on higher device tiers. Lighting is part of the Vibe system, so players need to see the result of their choices.

A soft rim light from behind the camera keeps characters readable even in dark, moody bars.

Emissive objects (neon, screens, jukebox) use bloom carefully. Bloom should feel warm and soft, never blown out.

Stage events add coloured stage lighting and a gentle spotlight, with the depth of field focused on the performer.

---

## 11. Characters

### 11.1 Look

Characters are chunky, rounded and friendly, with simplified hands (mitten-style with a thumb) and large heads for readability. Faces are minimal: dot eyes, small expressive eyebrows and simple mouth shapes. Eyebrows carry most of the emotion.

Characters have the same miniature material language as the environment: matte painted surfaces with soft bevels, and a hint of painted-figure texture, like a well-made tabletop miniature.

Diversity in body shape, skin tone, age, hair and clothing is required across customers, staff and the player-owner.

### 11.2 Readability

Each customer segment has a distinct silhouette and clothing language, so experienced players can read the crowd at a glance. For example, students wear hoodies and backpacks, young professionals wear tailored jackets, and music lovers wear band T-shirts and denim.

Staff wear a uniform that the player chooses (aprons, shirts, colours). Uniform colours should contrast clearly with customer clothing.

Mood is shown through body language first and a small floating icon second. Icons are drawn in the UI style and are never blurred.

### 11.3 Animation

Animation is bouncy, slightly exaggerated and full of personality, in the spirit of stop-motion figures. Characters squash slightly when they sit down, lean on the bar, and laugh with their whole bodies at the comedy night.

An optional subtle "stop-motion" effect (character animation stepped at around 12–15 frames per second while the camera and lighting stay smooth) is worth prototyping. If it tests well, it could become a signature of the style. If it harms readability or feels janky on mobile, it should be dropped.

---

## 12. Props & décor

### 12.1 Modelling rules

Every prop uses rounded bevels on all exposed edges and has slightly thickened forms (table tops, chair legs, shelves) so nothing looks fragile at small sizes.

Detail is expressed through shape and colour, not texture. A pint glass is a rounded resin form with amber liquid and a cream foam cap, not a detailed glass with reflections and condensation.

Props should be recognisable from the default camera angle and at the smallest supported phone size. Every new prop is tested at that size before approval.

### 12.2 Themes and colour variants

Props are built to support colour variants through masks or colour-ID channels, so one chair model can appear in many themes. Each prop belongs to one or more themes (traditional pub, speakeasy, tiki bar and so on), and its default colours come from that theme's palette.

### 12.3 Theme interpretation examples

| Theme | How it reads as a miniature |
| --- | --- |
| Traditional pub | Dark wood, brass rails, patterned carpet like printed fabric, a fire with a flickering orange glow |
| Speakeasy | Deep greens and burgundy, low amber lamps, velvet flocked booths, a hidden-door bookcase |
| Sports bar | Bright screens with simple animated "matches", pennants, high tables, team scarves in invented colours |
| Tiki bar | Bamboo and thatch made of fibre texture, carved forms, coloured paper lanterns |
| Craft beer taproom | Exposed brick, steel, chalkboard tap list, rows of colourful tap handles |
| Live music venue | Small raised stage, speaker stacks, coloured stage lights, gig posters |

---

## 13. Effects (VFX)

Effects follow the miniature language. They should look like the tricks a model-maker or stop-motion animator would use.

Steam and smoke are soft cotton-wool puffs. Spills are glossy resin puddles. Confetti and celebration effects are tiny paper pieces. Rain falls as fine clear threads, and snow looks like model snow (white flock) that gently settles on the plinth and the pavement outside.

Neon, candles and fairy lights use soft bloom with gentle flicker. Incidents (a dropped tray, a broken glass) use small, playful pops rather than realistic shattering.

Effects must remain readable and must never cover customers' problem icons or key interface elements.

---

## 14. Relationship with UI and branding

The UI and marketing use the mid-century poster style described in the mood board: flat graphic shapes, a tight palette and printed grain. This contrasts deliberately with the 3D diorama.

The two styles meet through the idea of printed materials around a model. Menus, event flyers, staff cards and nightly reports look like printed cards, labels and posters that a bar owner would pin up. The plinth plaque, price tags and labels in the world can borrow typography and colours from the UI so the two styles feel related.

UI elements never sit inside the depth-of-field blur, and icons in the world (problems, moods, alerts) use the UI style so they always read as interface rather than scenery.

---

## 15. Photo mode & sharing

Photo mode is a core feature of this style and should launch with the game. It includes free camera orbit, zoom and pitch; adjustable tilt-shift strength and focal point; time-of-day and lighting presets (daytime, golden hour, Service night, closing time); the option to hide or show customers, staff and the plinth; and frames, filters and stickers drawn in the UI style.

Every shared image includes the bar name on the plaque and a subtle LOAB watermark with a link or code to visit the bar.

---

## 16. Technical direction (Unity, URP)

### 16.1 Rendering features

The diorama look depends on a small number of rendering features, each of which must be scalable.

The tilt-shift effect is a custom URP renderer feature that blends a blurred copy of the scene with the sharp scene using a screen-space mask (top and bottom bands) plus depth information. A cheaper Gaussian blur is used on lower tiers, and higher tiers can use a depth-based bokeh blur.

The cutaway walls use a shader with height-based clipping, a procedural cap on the cut face, and a smooth animated cut height.

Lighting combines baked lighting for the fixed room shell with real-time lights for placed lamps, signs and stage lights. Light probes handle characters and movable objects. The number of real-time lights is capped per tier.

Ambient occlusion (screen-space on higher tiers, baked on lower tiers) adds contact shadows that make props feel grounded.

Bloom is applied to emissive surfaces only, using a threshold that keeps general surfaces clean.

### 16.2 Device tiers

| Feature | Low (older phones, WebGL) | Medium (mid-range phones) | High (flagship, PC) |
| --- | --- | --- | --- |
| Tilt-shift | Cheap screen-space gradient blur, or off | Gaussian, half resolution | Depth-aware bokeh |
| Real-time lights | 4 | 8 | 16+ |
| Shadows | Baked only, blob shadows for characters | One real-time shadow light | Multiple real-time shadows |
| Ambient occlusion | Baked | Baked plus simple SSAO | Full SSAO |
| Bloom | Off or very light | On, low quality | On, high quality |
| Character animation | Reduced crowd LOD | Standard | Full |
| Target frame rate | 30 fps | 30–60 fps | 60 fps |

The diorama identity must survive the low tier. On the lowest settings, the frame (plinth, cut edges, studio background), the narrow field of view and the warm practical lighting still carry the miniature look even without blur.

### 16.3 Asset budgets (indicative, to be validated in prototype)

| Asset type | Triangle budget | Texture approach |
| --- | --- | --- |
| Character (full detail) | 3,000–5,000 | Shared atlas, 512 px, colour masks |
| Character (crowd LOD) | 800–1,500 | Shared atlas |
| Small prop (glass, bottle) | 100–400 | Shared trim sheet |
| Medium prop (chair, stool, table) | 400–1,500 | Shared trim sheet plus colour mask |
| Large prop (bar counter, stage) | 1,500–4,000 | Trim sheet plus small unique detail map |
| Room shell (per module) | 500–2,000 | Tiling materials plus shared grain detail |

Shared trim sheets and a global "miniature grain" detail texture keep memory low and the look consistent.

---

## 17. Do & don't

| Do | Don't |
| --- | --- |
| Bevel every visible edge | Leave sharp, razor-thin CG edges |
| Use soft matte surfaces with a few glossy hero materials | Make everything shiny or photographic |
| Keep customers more saturated than backgrounds | Let characters disappear into busy interiors |
| Make warm practical lights the stars of every evening scene | Light interiors flatly with a single global light |
| Use a narrow field of view and a distant camera | Use a wide-angle lens that breaks the miniature illusion |
| Keep the frame (plinth, cut edges, studio) consistent | Recolour the frame per theme or hide it |
| Keep icons and alerts crisp and outside the blur | Blur anything the player needs to act on |
| Show unbuilt areas as clean white card | Use fog, darkness or empty voids for locked areas |
| Use model-maker effects (cotton wool, resin, flock) | Use realistic particle smoke, fire or shattering glass |
| Test every asset at the smallest phone size | Approve assets only at desktop resolution |

---

## 18. References

**Games:** *Townscaper* and *Tiny Glade* (handmade, toy-like construction and presentation), *Unpacking* (intimate personal spaces and collectable detail), *Captain Toad: Treasure Tracker* (cube dioramas and camera control), *Two Point Hospital* (chunky readable characters and humour), *LittleBigPlanet* (craft materials and handmade charm).

**Film and photography:** tilt-shift photography of real cities, Wes Anderson's cutaway sets and symmetrical framing, stop-motion films from studios such as Aardman and Laika for character materials and animation feel.

**Real-world crafts:** model railways and scenic modelling (flock, foam trees, resin water), dolls' houses and room boxes, architectural massing models (white card for unbuilt areas), and painted tabletop miniatures for character finish.

The art team should assemble these into a shared reference board, with annotations explaining exactly what to take from each.

---

## 19. Deliverables & next steps

| Step | Deliverable | Purpose |
| --- | --- | --- |
| 1 | Reference board | Shared visual language for the team |
| 2 | Three style frames (Prep daytime, Service night, comedy night event) | Lock the look before production |
| 3 | Shader prototype: tilt-shift, cutaway walls, miniature grain | Prove the effects run on low-tier devices and WebGL |
| 4 | Test kit: one room shell, 10 props, 3 characters | Validate budgets, readability and pipeline |
| 5 | Phone readability test | Confirm customers, problems and icons read at minimum size |
| 6 | Plinth and frame set (starter to legendary) | Lock the progression look |
| 7 | Style guide v1 | Production-ready rules for internal and outsourced artists |

---

## 20. Open questions

1. Should the stop-motion animation effect be part of the core look, an option, or dropped? This needs a prototype and playtest.
2. How far can the camera pitch go before the cutaway walls look wrong? Test limits in the shader prototype.
3. Should outdoor areas (beer gardens, rooftops) extend beyond the plinth, or should the plinth grow with the bar?
4. How should multi-venue players see their group: separate models on a shelf, or a city map of tiny plinths? The "shelf of models" idea is a strong candidate for the bar group screen.
5. In co-op and High Street modes, should neighbouring player bars appear as full models on a shared street plinth, or as simplified versions for performance?

---

*End of visual brief.*