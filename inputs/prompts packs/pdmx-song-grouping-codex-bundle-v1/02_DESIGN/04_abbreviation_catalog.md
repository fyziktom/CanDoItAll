# Abbreviation And Canonicalization Catalog

This catalog is deliberately practical rather than academically exhaustive.
It is optimized for score/work metadata seen in PDMX-like title strings.

## Classification legend

- **Safe** = reasonable for automatic derived normalization
- **Validate** = probably safe, but verify on real corpus first
- **Risky** = do not use as a silent hard-normalization rule in strict grouping

## A. Work / numbering tokens

| Source forms | Canonical derived form | Class | Notes |
|---|---|---:|---|
| `op`, `op.`, `opus` | `opus` | Safe | Strong classical signal |
| `no`, `no.`, `nr`, `nr.`, `num`, `number` | `number` | Safe | Keep as structured numbering token |
| `bk`, `book` | `book` | Safe | |
| `vol`, `vol.`, `volume` | `volume` | Safe | |
| `pt`, `pt.`, `part` | `part` | Safe | |
| `mov`, `mvt`, `mvmt`, `movement` | `movement` | Safe | |
| `var`, `var.`, `vars`, `variation`, `variations` | `variation` | Safe | |
| `intro`, `introd.` | `introduction` | Validate | |
| `finale` | `finale` | Safe | Keep as semantic label |
| `suite` | `suite` | Safe | |
| `son.`, `sonata` | `sonata` | Validate | Only if token clearly denotes work type |
| `sym.`, `symph.`, `symphony` | `symphony` | Validate | |
| `conc.`, `concerto` | `concerto` | Validate | |
| `qt.`, `quartet` | `quartet` | Validate | |
| `trio` | `trio` | Safe | |
| `duo` | `duo` | Safe | |

## B. Common catalog systems

Keep the catalog prefix as identity-bearing structure. Do not erase it.

| Source forms | Canonical prefix | Class | Notes |
|---|---|---:|---|
| `BWV` | `bwv` | Safe | Bach-Werke-Verzeichnis |
| `K`, `K.`, `KV`, `K.V.` | `k` | Validate | Mozart/Köchel family; preserve raw form too |
| `Hob`, `Hob.` | `hob` | Safe | Haydn |
| `D`, `D.` | `d` | Validate | Schubert can be ambiguous with keys if parsed badly |
| `S`, `S.` | `s` | Validate | Liszt; contextual parsing required |
| `RV` | `rv` | Safe | Vivaldi |
| `TWV` | `twv` | Safe | Telemann |
| `WoO` | `woo` | Safe | Beethoven etc. |
| `Anh`, `Anh.` | `anh` | Safe | |
| `H`, `H.` | `h` | Validate | Hummel / others; contextual |
| `Sz`, `Sz.` | `sz` | Safe | Bartók |
| `L`, `L.` | `l` | Validate | Contextual |
| `B`, `B.` | `b` | Validate | Contextual |
| `M`, `M.` | `m` | Risky | Too ambiguous without catalog context |

## C. Key / tonality tokens

| Source forms | Canonical derived form | Class | Notes |
|---|---|---:|---|
| `maj`, `major` | `major` | Safe | |
| `min`, `minor` | `minor` | Safe | |
| `dur` | `major` | Validate | Language-specific |
| `moll` | `minor` | Validate | Language-specific |
| `majeur` | `major` | Validate | French |
| `mineur` | `minor` | Validate | French |
| `flat`, `b`, `♭` | `flat` | Validate | Must be token-aware, not every `b` |
| `sharp`, `#`, `♯` | `sharp` | Validate | Must be token-aware |
| `es`, `is` suffixes in German note names | structured note parsing | Risky | Needs language-aware parsing |
| bare uppercase key `C` | `c_major?` | Risky | Only infer major in auxiliary parsing, not strict |
| bare lowercase key `c` | `c_minor?` | Risky | Same reason |

## D. Arrangement / editorial / version tokens

| Source forms | Canonical derived form | Class | Notes |
|---|---|---:|---|
| `arr`, `arr.`, `arranged by` | `arrangement` | Safe | Keep as explicit modifier |
| `orch`, `orch.` | `orchestration` | Validate | |
| `transcr`, `transcr.`, `transcription` | `transcription` | Safe | |
| `rev`, `rev.` | `revision` | Validate | |
| `ed`, `ed.` | `edition` | Safe | |
| `urtext` | `urtext` | Safe | |
| `version` | `version` | Safe | |
| `excerpt` | `excerpt` | Safe | Important boundary token |
| `reduction` | `reduction` | Validate | |
| `piano reduction` | `piano_reduction` | Validate | |
| `vocal score` | `vocal_score` | Validate | |
| `study score` | `study_score` | Validate | |
| `facsimile` | `facsimile` | Validate | |

## E. Movement and sectional labels

These are useful as extracted markers, but should not alone determine same-work identity.

| Source forms | Canonical derived form | Class |
|---|---|---:|
| `allegro` | `allegro` | Safe |
| `adagio` | `adagio` | Safe |
| `andante` | `andante` | Safe |
| `andantino` | `andantino` | Safe |
| `presto` | `presto` | Safe |
| `larghetto` | `larghetto` | Safe |
| `largo` | `largo` | Safe |
| `menuet`, `minuet` | `minuet` | Validate |
| `scherzo` | `scherzo` | Safe |
| `finale` | `finale` | Safe |
| `rondo`, `rondò`, `rondo` | `rondo` | Validate |
| `intermezzo` | `intermezzo` | Safe |
| `theme and variations` variants | `theme_and_variations` | Validate |

## F. Multilingual generic work words

These help loose search and candidate generation. Use carefully in strict identity logic.

| Source forms | Canonical derived form | Class |
|---|---|---:|
| `sinfonia`, `symphonie`, `symphony` | `symphony` | Validate |
| `sonate`, `sonata` | `sonata` | Validate |
| `concerto`, `konzert`, `concert` | `concerto` | Validate |
| `suite`, `suite` | `suite` | Safe |
| `partita` | `partita` | Safe |
| `prelude`, `prélude`, `preludio` | `prelude` | Validate |
| `fugue`, `fuga` | `fugue` | Validate |
| `waltz`, `walzer`, `valse` | `waltz` | Validate |
| `mazurka`, `mazurca` | `mazurka` | Validate |
| `polonaise`, `polacca` | `polonaise` | Validate |
| `impromptu` | `impromptu` | Safe |
| `nocturne`, `notturno` | `nocturne` | Validate |

## G. Composer-name normalization patterns

### Safe patterns

- trim/collapse whitespace
- lowercase in derived keys
- accent-insensitive loose key
- convert `Lastname, Firstname` -> `Firstname Lastname`
- normalize punctuation in initials:
  - `J. S. Bach`
  - `J S Bach`
  - `J.S.Bach`
  -> same loose initials pattern

### Validate patterns

- native vs anglicized first-name aliases:
  - `Frédéric` / `Frederic` / `Fryderyk`
  - `Pyotr` / `Petr` / `Peter`
  - `Nikolai` / `Nicolas`
- abbreviated forenames:
  - `Joh. Seb. Bach` -> `johann sebastian bach`
- particles:
  - `van`
  - `von`
  - `de`
  - `di`
  - `da`

### Risky patterns

- broad nickname dictionaries
- collapsing all same-surname composers
- assuming initials uniquely identify a composer
- removing particles from surnames indiscriminately

## H. Practical structured extraction targets

Codex should not only map tokens. It should extract structured fields when possible.

Desired extracted examples:
- `opus=27`
- `number=2`
- `catalog_system=k`
- `catalog_value=331`
- `movement=3`
- `key=d_flat_major`
- `work_type=nocturne`
- `has_arrangement_marker=true`
- `has_excerpt_marker=true`

## I. Examples of safe vs unsafe outcomes

### Safe equivalent pair

- `Nocturne in D-flat major, Op. 27, No. 2`
- `Nocturne in D flat major opus 27 no 2`

Expected:
- same strict work signature,
- very high confidence same exact work.

### Review-required pair

- `Moonlight Sonata, third movement`
- `Piano Sonata No. 14 in C-sharp minor, Op. 27 No. 2`

Expected:
- likely related,
- but not auto-merged into the same exact-work group unless movement boundary is modelled explicitly and the policy says to do so.

### Not same exact-work group

- `Symphony No. 5 in C minor, Op. 67`
- `Symphony No. 5 in C minor, Op. 67, arr. for piano four hands`

Expected:
- related family maybe,
- not silently collapsed into the same exact-work group.

## J. Implementation note

The catalog above should drive:
- normalization utilities,
- parser/tokenizer tests,
- evidence strings,
- search alias generation.

It should **not** become an uncontrolled “replace text globally” system.
