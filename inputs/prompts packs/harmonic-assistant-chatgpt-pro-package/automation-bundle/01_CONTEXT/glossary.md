# Glossary

- **Pitch class (PC)**: note modulo octave (C=0, C#=1, ..., B=11).
- **Chord symbol**: library symbol string, e.g. `maj`, `min`, `7`, `m7b5`, etc.
- **Dominant 7**: chord quality often represented by symbol `7`.
- **Arpeggiation**: playing chord tones sequentially (not all held simultaneously).
- **Melody noise**: non-chord tones played concurrently (typically right hand) that should not dominate chord detection.
- **Score / scoring**: a floating importance weight assigned to each pitch class based on recent playing, decaying over time.
- **Decay**: automatic reduction of score over time to “forget” old notes.
- **Hold weighting**: additional score accumulation for notes held down continuously.
- **Confidence threshold**: adjustable criterion for deciding whether chord detection is reliable enough.
- **Circle of fifths**: ordering of pitch classes by fifth motion; used as a tonal-distance feature.
- **Route planning**: generating and ranking candidate next-chord paths.
- **Beam search**: bounded search that keeps the top K partial paths at each depth.

