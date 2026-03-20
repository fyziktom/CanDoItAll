namespace MusicTheory.Core.Models;

public enum NoteLetter
{
    C = 0,
    D = 1,
    E = 2,
    F = 3,
    G = 4,
    A = 5,
    B = 6
}

public enum Accidental
{
    Flat = -1,
    Natural = 0,
    Sharp = 1
}

public enum ClefType
{
    Treble = 0,
    Bass = 1,
    Mixed = 2
}

public enum PracticeMode
{
    NoteIdentification = 0,
    ChordIdentification = 1,
    ProgressionPractice = 2,
    IntervalReading = 3
}

public enum DifficultyPreset
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2
}

public enum EnharmonicPreference
{
    Sharps = 0,
    Flats = 1
}

public enum ChordCategory
{
    Triad = 0,
    Sixth = 1,
    Seventh = 2,
    Extended = 3,
    AddedTone = 4,
    AlteredDominant = 5,
    Power = 6
}

public enum MusicalContextType
{
    Chord = 0,
    Scale = 1
}

public enum ModeType
{
    Major = 0,
    NaturalMinor = 1,
    Dorian = 2,
    Phrygian = 3,
    Lydian = 4,
    Mixolydian = 5,
    Locrian = 6,
    HarmonicMinor = 7,
    MelodicMinor = 8,
    MajorPentatonic = 9,
    MinorPentatonic = 10,
    BluesMinor = 11,
    BluesMajor = 12
}

public enum RomanNumeralDegree
{
    I = 1,
    II = 2,
    III = 3,
    IV = 4,
    V = 5,
    VI = 6,
    VII = 7
}

public enum ChordQuality
{
    Major = 0,
    Minor = 1,
    Diminished = 2,
    Augmented = 3,
    Sus2 = 4,
    Sus4 = 5,
    HalfDiminished = 6,
    Power5 = 7
}

public enum RomanSeventhType
{
    None = 0,
    Dominant = 1,
    Major = 2,
    Minor = 3,
    HalfDiminished = 4,
    Diminished = 5
}

public enum HarmonicFunctionTag
{
    Tonic = 0,
    Predominant = 1,
    Dominant = 2
}

public enum InversionType
{
    Root = 0,
    First = 1,
    Second = 2,
    Third = 3,
    Unknown = 4
}

public enum VoiceLeadingMode
{
    None = 0,
    Basic = 1,
    Optimal = 2
}

public enum IntervalQuality
{
    Perfect = 0,
    Major = 1,
    Minor = 2,
    Augmented = 3,
    Diminished = 4
}

public enum IntervalDirection
{
    Ascending = 0,
    Descending = 1
}

public enum IntervalAnswerStyle
{
    NumberOnly = 0,
    QualityAndNumber = 1,
    FullName = 2
}

public enum ProgressionMoodPreset
{
    PopBright = 0,
    PopMelancholic = 1,
    JazzStandard = 2,
    ClassicalFunctional = 3,
    RomanticColor = 4
}

public enum ProgressionModeScope
{
    Major = 0,
    Minor = 1,
    Both = 2
}

public enum ProgressionPatternStepKind
{
    DiatonicDegree = 0,
    RelativeRoot = 1,
    SecondaryDominant = 2,
    NeapolitanSixth = 3
}

public enum CadenceType
{
    None = 0,
    Authentic = 1,
    Plagal = 2,
    Deceptive = 3,
    Turnaround = 4
}

public enum ChromaticismLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ModulationMode
{
    None = 0,
    Pivot = 1,
    Direct = 2,
    CircleOfFifths = 3
}

public enum FormType
{
    ThroughComposed = 0,
    VerseChorus = 1,
    AABA = 2,
    VerseChorusBridgeChorus = 3
}

public enum ModulationPlanType
{
    None = 0,
    ChorusUpStep = 1,
    RelativeMajorMinor = 2,
    PivotToDominant = 3,
    CircleOfFifthsTravel = 4
}

public enum ProgressionSectionType
{
    Through = 0,
    Verse = 1,
    Chorus = 2,
    Bridge = 3,
    A = 4,
    B = 5
}

public enum StylePackPreset
{
    PopSimple = 0,
    SmoothJazzHotel = 1,
    BebopStandardJazz = 2,
    RomanticClassicalColor = 3,
    GospelSoulPop = 4
}

public enum HarmonicStyleTag
{
    Pop = 0,
    Jazz = 1,
    Classical = 2,
    Romantic = 3,
    Gospel = 4,
    Smooth = 5
}

[Flags]
public enum HarmonicDeviceGroup
{
    None = 0,
    SecondaryDominants = 1 << 0,
    TritoneSubstitutions = 1 << 1,
    BackdoorDominants = 1 << 2,
    DiminishedPassing = 1 << 3,
    TurnaroundVariants = 1 << 4,
    ModalInterchange = 1 << 5,
    Neapolitan = 1 << 6,
    AugmentedSixth = 1 << 7,
    ChromaticMediants = 1 << 8,
    CommonToneDiminished = 1 << 9,
    GospelColor = 1 << 10,
    PassingApproachDominants = 1 << 11,
    ModulationBridges = 1 << 12,
    All = SecondaryDominants |
          TritoneSubstitutions |
          BackdoorDominants |
          DiminishedPassing |
          TurnaroundVariants |
          ModalInterchange |
          Neapolitan |
          AugmentedSixth |
          ChromaticMediants |
          CommonToneDiminished |
          GospelColor |
          PassingApproachDominants |
          ModulationBridges
}

public enum LeadSheetVoicingStrategy
{
    Beginner = 0,
    Jazz = 1,
    Romantic = 2
}

public enum ChordVocabularyProfile
{
    Triads = 0,
    Sevenths = 1,
    Extensions = 2
}
