﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.SubtitleFormats
{
    /// <summary>
    /// http://www.whatwg.org/specs/web-apps/current-work/webvtt.html
    /// </summary>
    public class WebVTT : SubtitleFormat
    {

        private static readonly Regex RegexTimeCodes = new Regex(@"^-?\d+:-?\d+:-?\d+\.-?\d+\s*-->\s*-?\d+:-?\d+:-?\d+\.-?\d+", RegexOptions.Compiled);
        private static readonly Regex RegexTimeCodesMiddle = new Regex(@"^-?\d+:-?\d+\.-?\d+\s*-->\s*-?\d+:-?\d+:-?\d+\.-?\d+", RegexOptions.Compiled);
        private static readonly Regex RegexTimeCodesShort = new Regex(@"^-?\d+:-?\d+\.-?\d+\s*-->\s*-?\d+:-?\d+\.-?\d+", RegexOptions.Compiled);

        public override string Extension => ".vtt";

        public override string Name => "WebVTT";

        public override string ToText(Subtitle subtitle, string title)
        {
            const string timeCodeFormatHours = "{0:00}:{1:00}:{2:00}.{3:000}"; // hh:mm:ss.mmm
            const string paragraphWriteFormat = "{0} --> {1}{2}{5}{3}{4}{5}";

            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT");
            sb.AppendLine();
            foreach (Paragraph p in subtitle.Paragraphs)
            {
                string start = string.Format(timeCodeFormatHours, p.StartTime.Hours, p.StartTime.Minutes, p.StartTime.Seconds, p.StartTime.Milliseconds);
                string end = string.Format(timeCodeFormatHours, p.EndTime.Hours, p.EndTime.Minutes, p.EndTime.Seconds, p.EndTime.Milliseconds);
                string positionInfo = GetPositionInfoFromAssTag(p);

                string style = string.Empty;
                if (!string.IsNullOrEmpty(p.Extra) && subtitle.Header == "WEBVTT")
                    style = p.Extra;
                sb.AppendLine(string.Format(paragraphWriteFormat, start, end, positionInfo, FormatText(p), style, Environment.NewLine));
            }
            return sb.ToString().Trim();
        }

        internal static string GetPositionInfoFromAssTag(Paragraph p)
        {
            string positionInfo = string.Empty;

            if (p.Text.StartsWith("{\\a", StringComparison.Ordinal))
            {
                string position = null; // horizontal
                if (p.Text.StartsWith("{\\an1}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an4}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an7}", StringComparison.Ordinal)) // advanced sub station alpha
                {
                    position = "20%"; //left
                }
                else if (p.Text.StartsWith("{\\an3}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an6}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an9}", StringComparison.Ordinal)) // advanced sub station alpha
                {
                    position = "80%"; //right
                }

                string line = null;
                if (p.Text.StartsWith("{\\an7}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an8}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an9}", StringComparison.Ordinal)) // advanced sub station alpha
                {
                    line = "20%"; //top
                }
                else if (p.Text.StartsWith("{\\an4}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an5}", StringComparison.Ordinal) || p.Text.StartsWith("{\\an6}", StringComparison.Ordinal)) // advanced sub station alpha
                {
                    line = "50%"; //middle
                }

                if (!string.IsNullOrEmpty(position))
                {
                    positionInfo = " position:" + position;
                }
                if (!string.IsNullOrEmpty(line))
                {
                    positionInfo += " line:" + line;
                }
            }

            return positionInfo;
        }

        internal static string FormatText(Paragraph p)
        {
            string text = Utilities.RemoveSsaTags(p.Text);
            while (text.Contains(Environment.NewLine + Environment.NewLine))
                text = text.Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine);

            text = ColorHtmlToWebVtt(text);
            return text;
        }

        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            _errorCount = 0;
            Paragraph p = null;
            string positionInfo = string.Empty;
            bool hadEmptyLine = false;
            int numbers = 0;
            for (var index = 0; index < lines.Count; index++)
            {
                string line = lines[index];
                string next = string.Empty;
                if (index < lines.Count - 1)
                    next = lines[index + 1];
                var s = line;
                bool isTimeCode = line.Contains("-->");
                if (isTimeCode && RegexTimeCodesMiddle.IsMatch(s))
                {
                    s = "00:" + s; // start is without hours, end is with hours
                }

                if (isTimeCode && RegexTimeCodesShort.IsMatch(s))
                {
                    s = "00:" + s.Replace("--> ", "--> 00:");
                }

                if (isTimeCode && RegexTimeCodes.IsMatch(s.TrimStart()))
                {
                    if (p != null)
                    {
                        p.Text = p.Text.TrimEnd();
                        subtitle.Paragraphs.Add(p);
                    }

                    try
                    {
                        var parts = s.TrimStart().Replace("-->", "@").Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
                        p = new Paragraph
                        {
                            StartTime = GetTimeCodeFromString(parts[0]),
                            EndTime = GetTimeCodeFromString(parts[1])
                        };
                        positionInfo = GetPositionInfo(s);
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine(exception.Message);
                        _errorCount++;
                        p = null;
                    }

                    hadEmptyLine = false;
                }
                else if (subtitle.Paragraphs.Count == 0 && line.Trim() == "WEBVTT")
                {
                    subtitle.Header = "WEBVTT";
                }
                else if (p != null && hadEmptyLine && Utilities.IsInteger(line) &&
                         (RegexTimeCodesMiddle.IsMatch(next) ||
                          RegexTimeCodesShort.IsMatch(next) ||
                          RegexTimeCodes.IsMatch(next)))
                {
                    numbers++;
                }
                else if (p != null)
                {
                    string text = positionInfo + line.Trim();
                    if (string.IsNullOrEmpty(text))
                        hadEmptyLine = true;
                    if (string.IsNullOrEmpty(p.Text))
                        p.Text = text + Environment.NewLine;
                    else
                        p.Text += text + Environment.NewLine;
                    positionInfo = string.Empty;
                }
            }

            if (p != null)
            {
                p.Text = p.Text.TrimEnd();
                subtitle.Paragraphs.Add(p);
            }

            if (subtitle.Paragraphs.Count > 5 &&
                numbers >= subtitle.Paragraphs.Count - 1 &&
                lines[0] == "WEBVTT FILE")
            {
                // let format WebVTTFileWithLineNumber take the subtitle
                _errorCount = subtitle.Paragraphs.Count + 1;
                return;
            }

            foreach (var paragraph in subtitle.Paragraphs)
            {
                paragraph.Text = ColorWebVttToHtml(paragraph.Text);
                paragraph.Text = System.Net.WebUtility.HtmlDecode(paragraph.Text);
            }

            subtitle.Renumber();
        }

        internal static string GetPositionInfo(string s)
        {
            //position: x --- 0% = left, 100%=right (horizontal)
            //line: x --- 0 or -16 or 0%=top, 16 or -1 or 100% = bottom (vertical)
            var pos = GetTag(s, "position:");
            var line = GetTag(s, "line:");
            var positionInfo = string.Empty;
            bool hAlignLeft = false;
            bool hAlignRight = false;
            bool vAlignTop = false;
            bool vAlignMiddle = false;

            if (!string.IsNullOrEmpty(pos) && pos.EndsWith('%'))
            {
                if (double.TryParse(pos.TrimEnd('%'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                {
                    if (number < 25)
                    {
                        hAlignLeft = true;
                    }
                    else if (number > 75)
                    {
                        hAlignRight = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(line) && line.EndsWith('%'))
            {
                if (line.EndsWith('%'))
                {
                    if (double.TryParse(line.TrimEnd('%'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        if (number < 25)
                        {
                            vAlignTop = true;
                        }
                        else if (number < 75)
                        {
                            vAlignMiddle = true;
                        }
                    }
                }
                else
                {
                    if (double.TryParse(line.TrimEnd('%'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        if (number < 7)
                        {
                            vAlignTop = true;
                        }
                        else if (number < 11)
                        {
                            vAlignMiddle = true;
                        }
                    }
                }
            }

            if (hAlignLeft)
            {
                if (vAlignTop)
                {
                    return "{\\an7}";
                }
                if (vAlignMiddle)
                {
                    return "{\\an4}";
                }
                return "{\\an1}";
            }

            if (hAlignRight)
            {
                if (vAlignTop)
                {
                    return "{\\an9}";
                }
                if (vAlignMiddle)
                {
                    return "{\\an6}";
                }
                return "{\\an3}";
            }

            if (vAlignTop)
            {
                return "{\\an8}";
            }

            if (vAlignMiddle)
            {
                return "{\\an5}";
            }

            return positionInfo;
        }

        private static string GetTag(string s, string tag)
        {
            var pos = s.IndexOf(tag, StringComparison.Ordinal);
            if (pos >= 0)
            {
                var v = s.Substring(pos + tag.Length).Trim();
                var end = v.IndexOf("%,", StringComparison.Ordinal);
                if (end >= 0)
                {
                    v = v.Remove(end + 1);
                }
                end = v.IndexOf(' ');
                if (end >= 0)
                {
                    v = v.Remove(end);
                }
                return v;
            }
            return null;
        }

        public override void RemoveNativeFormatting(Subtitle subtitle, SubtitleFormat newFormat)
        {
            var regexRemoveCTags = new Regex(@"\</?c([a-zA-Z\._]+)\>", RegexOptions.Compiled);
            foreach (Paragraph p in subtitle.Paragraphs)
            {
                if (p.Text.Contains('<'))
                {
                    string text = p.Text;
                    text = RemoveTag("v", text);
                    text = RemoveTag("rt", text);
                    text = RemoveTag("ruby", text);
                    text = RemoveTag("span", text);
                    text = regexRemoveCTags.Replace(text, string.Empty).Trim();
                    p.Text = text;
                }
            }
        }

        private static readonly Regex RegexWebVttColor = new Regex(@"<c.[a-z]*>", RegexOptions.Compiled);

        internal static string ColorWebVttToHtml(string text)
        {
            text = text.Replace("</c>", "</font>");
            var match = RegexWebVttColor.Match(text);
            while (match.Success)
            {
                var fontString = "<font color=\"" + match.Value.Substring(3, match.Value.Length - 4) + "\">";
                fontString = fontString.Trim('"').Trim('\'');
                text = text.Remove(match.Index, match.Length).Insert(match.Index, fontString);
                match = RegexWebVttColor.Match(text);
            }
            return text;
        }

        private static readonly Regex RegexHtmlColor = new Regex("<font color=\"[a-z]*\">", RegexOptions.Compiled);
        private static readonly Regex RegexHtmlColor2 = new Regex("<font color=[a-z]*>", RegexOptions.Compiled);

        private static string ColorHtmlToWebVtt(string text)
        {
            text = text.Replace("</font>", "</c>");
            var match = RegexHtmlColor.Match(text);
            while (match.Success)
            {
                var fontString = "<c." + match.Value.Substring(13, match.Value.Length - 15) + ">";
                fontString = fontString.Trim('"').Trim('\'');
                text = text.Remove(match.Index, match.Length).Insert(match.Index, fontString);
                match = RegexHtmlColor.Match(text);
            }
            match = RegexHtmlColor2.Match(text);
            while (match.Success)
            {
                var fontString = "<c." + match.Value.Substring(12, match.Value.Length - 13) + ">";
                fontString = fontString.Trim('"').Trim('\'');
                text = text.Remove(match.Index, match.Length).Insert(match.Index, fontString);
                match = RegexHtmlColor2.Match(text);
            }
            return text;
        }

        public static List<string> GetVoices(Subtitle subtitle)
        {
            var list = new List<string>();
            if (subtitle != null && subtitle.Paragraphs != null)
            {
                foreach (Paragraph p in subtitle.Paragraphs)
                {
                    string s = p.Text;
                    var startIndex = s.IndexOf("<v ", StringComparison.Ordinal);
                    while (startIndex >= 0)
                    {
                        int endIndex = s.IndexOf('>', startIndex);
                        if (endIndex > startIndex)
                        {
                            string voice = s.Substring(startIndex + 2, endIndex - startIndex - 2).Trim();
                            if (!list.Contains(voice))
                                list.Add(voice);
                        }

                        if (startIndex == s.Length - 1)
                            startIndex = -1;
                        else
                            startIndex = s.IndexOf("<v ", startIndex + 1, StringComparison.Ordinal);
                    }
                }
            }
            return list;
        }

        public static string RemoveTag(string tag, string text)
        {
            int indexOfTag = text.IndexOf("<" + tag + " ", StringComparison.Ordinal);
            if (indexOfTag >= 0)
            {
                int indexOfEnd = text.IndexOf('>', indexOfTag);
                if (indexOfEnd > 0)
                {
                    text = text.Remove(indexOfTag, indexOfEnd - indexOfTag + 1);
                    text = text.Replace("</" + tag + ">", string.Empty);
                }
            }
            return text;
        }

        internal static TimeCode GetTimeCodeFromString(string time)
        {
            // hh:mm:ss.mmm
            string[] timeCode = time.Trim().Split(':', '.', ' ');
            return new TimeCode(int.Parse(timeCode[0]),
                                int.Parse(timeCode[1]),
                                int.Parse(timeCode[2]),
                                int.Parse(timeCode[3]));
        }

    }
}
