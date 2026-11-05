using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// Reads the XML documentation file that the C# compiler writes next to an assembly, so the generated C++ keeps the
/// comments of the native ABI.
/// </summary>
internal sealed class XmlDocumentation
{
    /// <summary>
    /// The <c>&lt;member&gt;</c> elements by member ID, such as <c>F:MajSimai.Unmanaged.UnmanagedSimaiNote.type</c>.
    /// </summary>
    private readonly Dictionary<string, XElement> _members;

    /// <summary>
    /// Creates the reader.
    /// </summary>
    /// <param name="members">The <c>&lt;member&gt;</c> elements by member ID.</param>
    private XmlDocumentation(Dictionary<string, XElement> members)
    {
        _members = members;
    }

    /// <summary>
    /// Loads the documentation file of an assembly: the file with the same name and the <c>.xml</c> extension.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The documentation.</returns>
    /// <exception cref="CppGenException">Thrown when the file does not exist.</exception>
    public static XmlDocumentation Load(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        if (!File.Exists(path))
        {
            throw new CppGenException($"The documentation file '{path}' does not exist. Build {assembly.GetName().Name} with GenerateDocumentationFile enabled.");
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Reads documentation from XML text.
    /// </summary>
    /// <param name="xml">The content of a documentation file.</param>
    /// <returns>The documentation.</returns>
    public static XmlDocumentation Parse(string xml)
    {
        var members = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var member in XDocument.Parse(xml).Descendants("member"))
        {
            var name = (string?)member.Attribute("name");
            if (name is not null)
            {
                members[name] = member;
            }
        }

        return new XmlDocumentation(members);
    }

    /// <summary>
    /// Gets the documentation of a type, field or method.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The documentation, or <see cref="DocComment.Empty"/> when the member has none.</returns>
    public DocComment Get(MemberInfo member)
    {
        if (!_members.TryGetValue(GetId(member), out var element))
        {
            return DocComment.Empty;
        }
        var context = member as Type ?? member.DeclaringType;
        var contextName = context is null ? string.Empty : GetTypeName(context);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in element.Elements("param"))
        {
            var name = (string?)parameter.Attribute("name");
            if (name is not null)
            {
                parameters[name] = RenderText(parameter, contextName);
            }
        }
        var summary = element.Element("summary");
        var returns = element.Element("returns");

        return new DocComment(summary is null ? string.Empty : RenderText(summary, contextName),
                              parameters,
                              returns is null ? string.Empty : RenderText(returns, contextName));
    }

    /// <summary>
    /// Gets the documentation ID of a member, as the C# compiler writes it.
    /// </summary>
    /// <param name="member">A type, field or method.</param>
    /// <returns>The ID, such as <c>M:MajSimai.Unmanaged.UnmanagedSimaiParser.Free(System.IntPtr)</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when the member is not a type, field or method.</exception>
    public static string GetId(MemberInfo member)
    {
        return member switch
        {
            Type type => "T:" + GetTypeName(type),
            FieldInfo field => "F:" + GetTypeName(field.DeclaringType!) + "." + field.Name,
            MethodInfo method => "M:" + GetTypeName(method.DeclaringType!) + "." + method.Name + GetParameterList(method),
            _ => throw new ArgumentException($"Members of type {member.GetType().Name} are not supported.", nameof(member)),
        };
    }

    /// <summary>
    /// Turns the content of a documentation element into one line of plain text. References become the name they
    /// point to; a reference to a member of <paramref name="contextTypeName"/> keeps only the member name.
    /// </summary>
    /// <param name="element">The element, such as <c>&lt;summary&gt;</c>.</param>
    /// <param name="contextTypeName">The full name of the type the documentation belongs to.</param>
    /// <returns>The text, with whitespace collapsed.</returns>
    internal static string RenderText(XElement element, string contextTypeName)
    {
        var builder = new StringBuilder();
        AppendNodes(builder, element, contextTypeName);

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Appends the text of the child nodes of an element.
    /// </summary>
    /// <param name="builder">Receives the text.</param>
    /// <param name="element">The element.</param>
    /// <param name="contextTypeName">The full name of the type the documentation belongs to.</param>
    private static void AppendNodes(StringBuilder builder, XElement element, string contextTypeName)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child:
                    AppendElement(builder, child, contextTypeName);
                    break;
            }
        }
    }

    /// <summary>
    /// Appends the text of one documentation tag.
    /// </summary>
    /// <param name="builder">Receives the text.</param>
    /// <param name="element">The tag.</param>
    /// <param name="contextTypeName">The full name of the type the documentation belongs to.</param>
    private static void AppendElement(StringBuilder builder, XElement element, string contextTypeName)
    {
        var cref = (string?)element.Attribute("cref");
        var langword = (string?)element.Attribute("langword");
        var name = (string?)element.Attribute("name");
        if (cref is not null && !element.Nodes().Any())
        {
            builder.Append(RenderReference(cref, contextTypeName));
        }
        else if (langword is not null)
        {
            builder.Append(langword);
        }
        else if (name is not null && element.Name.LocalName is "paramref" or "typeparamref")
        {
            builder.Append(name);
        }
        else if (element.Name.LocalName is "para" or "list" or "item" or "br")
        {
            builder.Append(' ');
            AppendNodes(builder, element, contextTypeName);
            builder.Append(' ');
        }
        else
        {
            AppendNodes(builder, element, contextTypeName);
        }
    }

    /// <summary>
    /// Turns a documentation reference into a short name.
    /// </summary>
    /// <param name="cref">The reference, such as <c>P:MajSimai.SimaiNote.RelatedNotes</c>.</param>
    /// <param name="contextTypeName">The full name of the type the documentation belongs to.</param>
    /// <returns>
    /// The type name for a type, the member name for a member of the context type, and <c>Type.Member</c> otherwise.
    /// </returns>
    private static string RenderReference(string cref, string contextTypeName)
    {
        var colon = cref.IndexOf(':');
        var kind = colon == 1 ? cref[0] : 'T';
        var name = colon == 1 ? cref.Substring(2) : cref;
        var parameters = name.IndexOf('(');
        if (parameters >= 0)
        {
            name = name.Substring(0, parameters);
        }
        if (kind == 'T')
        {
            return LastSegment(name);
        }
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            return name;
        }
        var typeName = name.Substring(0, dot);
        var memberName = name.Substring(dot + 1);

        return typeName == contextTypeName ? memberName : LastSegment(typeName) + "." + memberName;
    }

    /// <summary>
    /// Gets the part of a dotted name after the last dot.
    /// </summary>
    /// <param name="name">The dotted name.</param>
    /// <returns>The last part.</returns>
    private static string LastSegment(string name)
    {
        return name.Substring(name.LastIndexOf('.') + 1);
    }

    /// <summary>
    /// Gets the parameter list of a method as the documentation ID writes it.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The list in parentheses, or an empty string for a method without parameters.</returns>
    private static string GetParameterList(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        return "(" + string.Join(",", parameters.Select(p => GetTypeName(p.ParameterType))) + ")";
    }

    /// <summary>
    /// Gets the name of a type as the documentation ID writes it.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The full name, with <c>*</c> for pointers and <c>.</c> between nested types.</returns>
    private static string GetTypeName(Type type)
    {
        if (type.IsPointer)
        {
            return GetTypeName(type.GetElementType()!) + "*";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }
}
