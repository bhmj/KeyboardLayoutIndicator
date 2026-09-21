using System.Collections.Generic;

namespace KeyboardLayoutIndicator.Keyboard
{
    /// <summary>
    /// Определяет, относится ли клавиша к "символьным" — то есть к таким, чьё
    /// печатаемое значение меняется при переключении раскладки (буквы,
    /// цифровой ряд и соседствующие с ним OEM-клавиши с доп. знаками).
    /// Служебные клавиши (стрелки, Shift, Alt, Ctrl, Win, Tab, Enter, Esc,
    /// функциональные F1-F24, Caps/Num/Scroll Lock и т.д.) под это определение
    /// не попадают и в набор ниже не включены.
    ///
    /// Виртуальные коды (vkCode) в Windows назначаются по позиции клавиши
    /// на стандартной раскладке US, а не по фактически печатаемому символу,
    /// поэтому один и тот же диапазон кодов надёжно соответствует буквенным
    /// и цифровым клавишам независимо от текущей активной раскладки.
    /// </summary>
    public static class SymbolKeyClassifier
    {
        // 0x30-0x39: верхний цифровой ряд '0'..'9' (НЕ цифровой блок NumPad —
        // тот не зависит от раскладки, только от NumLock).
        // 0x41-0x5A: буквы A..Z.
        // 0xBA-0xC0, 0xDB-0xE2: OEM-клавиши пунктуации (;:, =+, ,<, -_, .>, /?,
        // `~, [{, \|, ]}, '", доп. клавиша OEM_8/OEM_102), значения которых
        // тоже меняются вместе с раскладкой (например, "ж" вместо ";" в ru-RU).
        private static readonly HashSet<uint> LayoutDependentKeys = new()
        {
            // цифровой ряд
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
            // буквы A-Z
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A,
            0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54,
            0x55, 0x56, 0x57, 0x58, 0x59, 0x5A,
            // OEM-пунктуация
            0xBA, // VK_OEM_1      ;:
            0xBB, // VK_OEM_PLUS   =+
            0xBC, // VK_OEM_COMMA  ,<
            0xBD, // VK_OEM_MINUS  -_
            0xBE, // VK_OEM_PERIOD .>
            0xBF, // VK_OEM_2      /?
            0xC0, // VK_OEM_3      `~
            0xDB, // VK_OEM_4      [{
            0xDC, // VK_OEM_5      \|
            0xDD, // VK_OEM_6      ]}
            0xDE, // VK_OEM_7      '"
            0xDF, // VK_OEM_8      (доп., зависит от раскладки/производителя)
            0xE2, // VK_OEM_102    доп. клавиша рядом с левым Shift (\| или <>)
        };

        public static bool IsLayoutDependentKey(uint vkCode) => LayoutDependentKeys.Contains(vkCode);
    }
}
