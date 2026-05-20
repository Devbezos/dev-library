namespace dev_library.Data.Discord
{
    public class GuildApplication
    {
        public int RowIndex { get; init; }  // 1-based sheet row number
        public bool IsPosted { get; init; }
        public DateTime Timestamp { get; init; }
        public string ContactInfo { get; init; }
        public string ClassSpec { get; init; }
        public string? Multiclassing { get; init; }
        public string CanMakeRaidTimes { get; init; }
        public string WarcraftLogs { get; init; }
        public string? PrivateLogCredentials { get; init; }
        public string MythicExperience { get; init; }
        public string ReasonForLeaving { get; init; }
        public string WhyReforged { get; init; }
        public string? AnythingElse { get; init; }

        public string ToDiscordMessage()
        {
            static string Blockquote(string text) =>
                string.Join("\n", text.Split('\n').Select(l => $"> {l}"));

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("## 📋 New Guild Application");
            sb.AppendLine($"**Contact / Character:** {ContactInfo}");
            sb.AppendLine($"**Class & Spec:** {ClassSpec}");

            if (!string.IsNullOrWhiteSpace(Multiclassing))
                sb.AppendLine($"**Multiclassing:** {Multiclassing}");

            sb.AppendLine($"**Raid Times (Mon–Thurs 8:45PM–12AM EST):** {CanMakeRaidTimes}");
            sb.AppendLine($"**Warcraft Logs:** {WarcraftLogs}");

            if (!string.IsNullOrWhiteSpace(PrivateLogCredentials))
                sb.AppendLine($"**Private Log Credentials:** {PrivateLogCredentials}");

            sb.AppendLine();
            sb.AppendLine("**Mythic Raiding Experience:**");
            sb.AppendLine(Blockquote(MythicExperience));

            sb.AppendLine();
            sb.AppendLine("**Reason for Leaving:**");
            sb.AppendLine(Blockquote(ReasonForLeaving));

            sb.AppendLine();
            sb.AppendLine("**Why Reforged:**");
            sb.AppendLine(Blockquote(WhyReforged));

            if (!string.IsNullOrWhiteSpace(AnythingElse))
            {
                sb.AppendLine();
                sb.AppendLine("**Anything Else:**");
                sb.AppendLine(Blockquote(AnythingElse));
            }

            sb.AppendLine();
            sb.Append($"-# Submitted {Timestamp:MMM d, yyyy 'at' h:mm tt} UTC");

            return sb.ToString();
        }
    }
}
