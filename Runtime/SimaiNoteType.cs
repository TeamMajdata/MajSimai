namespace MajSimai;

/// <summary>
/// The kind of a <see cref="SimaiNote"/>.
/// </summary>
public enum SimaiNoteType
{
    /// <summary>
    /// A tap note on a button, including the star or tap head of a slide.
    /// </summary>
    Tap,
    /// <summary>
    /// The path of a slide. The head of the slide is a separate <see cref="Tap"/> note that lists the slide in
    /// <see cref="SimaiNote.RelatedNotes"/>.
    /// </summary>
    Slide,
    /// <summary>
    /// A hold note on a button.
    /// </summary>
    Hold,
    /// <summary>
    /// A touch note on a sensor.
    /// </summary>
    Touch,
    /// <summary>
    /// A touch hold note on a sensor.
    /// </summary>
    TouchHold
}
