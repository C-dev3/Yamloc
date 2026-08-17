using YamlDotNet.Serialization;

namespace Yamloc
{
    /// <summary>
    /// A single localization entry, containing the translated message and
    /// a description of where it was used (for translator context).
    /// </summary>
    public class LocEntry
    {
        /// <summary>
        /// Gets or sets the localized (or fallback) message text.
        /// </summary>
        [YamlMember(Alias = "message")]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets a description of where this string is used, to give translators context.
        /// </summary>
        [YamlMember(Alias = "description")]
        public string Description { get; set; }
    }
}
