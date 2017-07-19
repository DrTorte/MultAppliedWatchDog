using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class UiTextEntry
    {
        public int Row;
        public int Column;
        private string TextInternal;
        public string Text { get { return TextInternal; } set { TextInternal = value; Clear = true; } }
        public bool Clear = false;

        public UiTextEntry(int _row, int _column, string _text)
        {
            Row = _row;
            Column = _column;
            Text = _text;
        }
    }

    class UiUpdateEntry
    {
        public string InternalText;
        public string Text { get { return InternalText; } set { InternalText = value; Update = true; } }
        public bool Update = false;

        public UiUpdateEntry(string __text)
        {
            Text = __text;
            Update = true;
        }
    }
}
