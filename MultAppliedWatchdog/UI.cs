using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class UITextEntry
    {
        public int row;
        public int column;
        public string text;
        public string newText;
        public bool clear = false;

        public UITextEntry(int _row, int _column, string _text)
        {
            row = _row;
            column = _column;
            text = _text;
        }

        public void setNewText(string text)
        {
            clear = true;
            newText = text;
        }
    }
}
