using MajSimai.Diagnostics;
using MajSimai.Logging;

namespace MajSimai.Parsing;

/// <summary>
/// What a parser does when a <see cref="SyntaxRule"/> is broken.
/// </summary>
internal enum SyntaxRuleKind
{
    /// <summary>The text cannot be read at all: parsing throws in every mode.</summary>
    Invalid,
    /// <summary>The text breaks the grammar or the grammar does not define it: strict mode throws, lenient mode skips
    /// it or reads it the MajSimai way and logs it.</summary>
    Undefined,
    /// <summary>The text is valid but questionable: parsing never throws, only the syntax checker reports it.</summary>
    Advice
}

/// <summary>
/// The exception type a broken <see cref="SyntaxRule"/> throws.
/// </summary>
internal enum SyntaxExceptionKind
{
    /// <summary><see cref="InvalidSimaiMarkupException"/>.</summary>
    Markup,
    /// <summary><see cref="InvalidSimaiSyntaxException"/>.</summary>
    Syntax,
    /// <summary><see cref="InvalidSimaiNoteException"/>.</summary>
    Note
}

/// <summary>
/// A rule of the simai grammar, and how each mode reacts when the rule is broken.
/// </summary>
internal sealed class SyntaxRule
{
    /// <summary>
    /// Creates a rule.
    /// </summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="kind">What a parser does when the rule is broken.</param>
    /// <param name="severity">The severity reported by the syntax checker.</param>
    /// <param name="exceptionKind">The exception type thrown when the rule stops parsing.</param>
    /// <param name="lenientLogLevel">The level logged in lenient mode for <see cref="SyntaxRuleKind.Undefined"/> rules.</param>
    public SyntaxRule(string code,
                      SyntaxRuleKind kind,
                      SimaiDiagnosticSeverity severity,
                      SyntaxExceptionKind exceptionKind,
                      SimaiLogLevel lenientLogLevel = SimaiLogLevel.Debug)
    {
        Code = code;
        Kind = kind;
        Severity = severity;
        ExceptionKind = exceptionKind;
        LenientLogLevel = lenientLogLevel;
    }

    /// <summary>Gets the diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets what a parser does when the rule is broken.</summary>
    public SyntaxRuleKind Kind { get; }

    /// <summary>Gets the severity reported by the syntax checker.</summary>
    public SimaiDiagnosticSeverity Severity { get; }

    /// <summary>Gets the exception type thrown when the rule stops parsing.</summary>
    public SyntaxExceptionKind ExceptionKind { get; }

    /// <summary>Gets the level logged in lenient mode for <see cref="SyntaxRuleKind.Undefined"/> rules.</summary>
    public SimaiLogLevel LenientLogLevel { get; }
}

/// <summary>
/// The rules of the simai grammar that MajSimai checks.
/// </summary>
internal static class SyntaxRules
{
    /// <summary>See <see cref="SimaiDiagnosticCodes.UnexpectedCharacter"/>.</summary>
    public static readonly SyntaxRule UnexpectedCharacter = new(SimaiDiagnosticCodes.UnexpectedCharacter, SyntaxRuleKind.Invalid, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Markup);

    /// <summary>See <see cref="SimaiDiagnosticCodes.UnclosedDeclaration"/>.</summary>
    public static readonly SyntaxRule UnclosedDeclaration = new(SimaiDiagnosticCodes.UnclosedDeclaration, SyntaxRuleKind.Invalid, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidDeclaration"/>, thrown as a syntax exception.</summary>
    public static readonly SyntaxRule InvalidDeclaration = new(SimaiDiagnosticCodes.InvalidDeclaration, SyntaxRuleKind.Invalid, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidDeclaration"/>, thrown as a markup exception like MajSimai 2.x did for speed values.</summary>
    public static readonly SyntaxRule InvalidSpeedValue = new(SimaiDiagnosticCodes.InvalidDeclaration, SyntaxRuleKind.Invalid, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Markup);

    /// <summary>See <see cref="SimaiDiagnosticCodes.NoteCutByDeclaration"/>.</summary>
    public static readonly SyntaxRule NoteCutByDeclaration = new(SimaiDiagnosticCodes.NoteCutByDeclaration, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.UnterminatedNote"/>.</summary>
    public static readonly SyntaxRule UnterminatedNote = new(SimaiDiagnosticCodes.UnterminatedNote, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.StrayCharacter"/>.</summary>
    public static readonly SyntaxRule StrayCharacter = new(SimaiDiagnosticCodes.StrayCharacter, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Markup);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidSignature"/>.</summary>
    public static readonly SyntaxRule InvalidSignature = new(SimaiDiagnosticCodes.InvalidSignature, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax);

    /// <summary>See <see cref="SimaiDiagnosticCodes.TextAfterEnd"/>.</summary>
    public static readonly SyntaxRule TextAfterEnd = new(SimaiDiagnosticCodes.TextAfterEnd, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.EmptyFakeEachPart"/>.</summary>
    public static readonly SyntaxRule EmptyFakeEachPart = new(SimaiDiagnosticCodes.EmptyFakeEachPart, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.EmptyNote"/>.</summary>
    public static readonly SyntaxRule EmptyNote = new(SimaiDiagnosticCodes.EmptyNote, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.MisplacedTouchArea"/>.</summary>
    public static readonly SyntaxRule MisplacedTouchArea = new(SimaiDiagnosticCodes.MisplacedTouchArea, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.EmptySameHeadPart"/>.</summary>
    public static readonly SyntaxRule EmptySameHeadPart = new(SimaiDiagnosticCodes.EmptySameHeadPart, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.MalformedDuration"/>.</summary>
    public static readonly SyntaxRule MalformedDuration = new(SimaiDiagnosticCodes.MalformedDuration, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.UnreadableNote"/>.</summary>
    public static readonly SyntaxRule UnreadableNote = new(SimaiDiagnosticCodes.UnreadableNote, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.UnreadableHoldLength"/>.</summary>
    public static readonly SyntaxRule UnreadableHoldLength = new(SimaiDiagnosticCodes.UnreadableHoldLength, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.IgnoredMarker"/>.</summary>
    public static readonly SyntaxRule IgnoredMarker = new(SimaiDiagnosticCodes.IgnoredMarker, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.EmptyEachPart"/>.</summary>
    public static readonly SyntaxRule EmptyEachPart = new(SimaiDiagnosticCodes.EmptyEachPart, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidNoteShape"/>.</summary>
    public static readonly SyntaxRule InvalidNoteShape = new(SimaiDiagnosticCodes.InvalidNoteShape, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidSlideEnd"/>.</summary>
    public static readonly SyntaxRule InvalidSlideEnd = new(SimaiDiagnosticCodes.InvalidSlideEnd, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidSlideEnd"/>, for <c>v</c> slides back to their start, which only Majdata draws.</summary>
    public static readonly SyntaxRule MajdataOnlySlideEnd = new(SimaiDiagnosticCodes.InvalidSlideEnd, SyntaxRuleKind.Advice, SimaiDiagnosticSeverity.Warning, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidSlidePath"/>.</summary>
    public static readonly SyntaxRule InvalidSlidePath = new(SimaiDiagnosticCodes.InvalidSlidePath, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InconsistentSlideDuration"/>.</summary>
    public static readonly SyntaxRule InconsistentSlideDuration = new(SimaiDiagnosticCodes.InconsistentSlideDuration, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.WifiInConnectedSlide"/>.</summary>
    public static readonly SyntaxRule WifiInConnectedSlide = new(SimaiDiagnosticCodes.WifiInConnectedSlide, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidSlideCode"/>.</summary>
    public static readonly SyntaxRule InvalidSlideCode = new(SimaiDiagnosticCodes.InvalidSlideCode, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.ImpossibleSlideCodeConnection"/>.</summary>
    public static readonly SyntaxRule ImpossibleSlideCodeConnection = new(SimaiDiagnosticCodes.ImpossibleSlideCodeConnection, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.ZeroLengthSlideCode"/>.</summary>
    public static readonly SyntaxRule ZeroLengthSlideCode = new(SimaiDiagnosticCodes.ZeroLengthSlideCode, SyntaxRuleKind.Advice, SimaiDiagnosticSeverity.Warning, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.ManyTurnsSlideCode"/>.</summary>
    public static readonly SyntaxRule ManyTurnsSlideCode = new(SimaiDiagnosticCodes.ManyTurnsSlideCode, SyntaxRuleKind.Advice, SimaiDiagnosticSeverity.Warning, SyntaxExceptionKind.Note);

    /// <summary>See <see cref="SimaiDiagnosticCodes.FieldWithoutValue"/>.</summary>
    public static readonly SyntaxRule FieldWithoutValue = new(SimaiDiagnosticCodes.FieldWithoutValue, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Markup, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.InvalidOffset"/>.</summary>
    public static readonly SyntaxRule InvalidOffset = new(SimaiDiagnosticCodes.InvalidOffset, SyntaxRuleKind.Undefined, SimaiDiagnosticSeverity.Error, SyntaxExceptionKind.Syntax, SimaiLogLevel.Warning);

    /// <summary>See <see cref="SimaiDiagnosticCodes.DuplicateField"/>.</summary>
    public static readonly SyntaxRule DuplicateField = new(SimaiDiagnosticCodes.DuplicateField, SyntaxRuleKind.Advice, SimaiDiagnosticSeverity.Warning, SyntaxExceptionKind.Markup);
}
