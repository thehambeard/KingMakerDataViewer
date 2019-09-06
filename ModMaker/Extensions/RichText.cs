using System.Linq;
using UnityEngine;

namespace ModMaker.Extensions
{
    public static class RichText
    {
        // https://docs.unity3d.com/Manual/StyledText.html

        public enum RGBA : uint
        {
            aqua = 0x00ffffff,
            black = 0x000000ff,
            blue = 0x0000ffff,
            brown = 0xa52a2aff,
            cyan = 0x00ffffff,
            darkblue = 0x0000a0ff,
            fuchsia = 0xff00ffff,
            green = 0x008000ff,
            grey = 0x808080ff,
            lightblue = 0xadd8e6ff,
            lime = 0x00ff00ff,
            magenta = 0xff00ffff,
            maroon = 0x800000ff,
            navy = 0x000080ff,
            olive = 0x808000ff,
            orange = 0xffa500ff,
            purple = 0x800080ff,
            red = 0xff0000ff,
            silver = 0xc0c0c0ff,
            teal = 0x008080ff,
            white = 0xffffffff,
            yellow = 0xffff00ff
        }

        public static string Bold(this string str)
        {
            return $"<b>{str}</b>";
        }

        public static string Color(this string str, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{str}</color>";
        }

        public static string Color(this string str, RGBA color)
        {
            return $"<color=#{color:X}>{str}</color>";
        }

        public static string Color(this string str, string rrggbbaa)
        {
            return $"<color=#{rrggbbaa}>{str}</color>";
        }

        public static string Italic(this string str)
        {
            return $"<i>{str}</i>";
        }

        public static string ToSentence(this string str)
        {
            return string.Concat(str.Select(c => char.IsUpper(c) ? " " + c : c.ToString())).TrimStart(' ');
        }

        public static string Size(this string str, int size)
        {
            return $"<size={size}>{str}</size>";
        }

        public static string SizePercent(this string str, int percent)
        {
            return $"<size={percent}%>{str}</size>";
        }
    }
}
