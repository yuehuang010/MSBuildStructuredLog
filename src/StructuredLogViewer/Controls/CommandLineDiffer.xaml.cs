using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// Interaction logic for CommandLineDiffer.xaml
    /// </summary>
    public partial class CommandLineDiffer : UserControl
    {
        public CommandLineDiffer()
        {
            InitializeComponent();
        }

        public void Initialize(string cmdLine)
        {
            textEditorLeft.Text = cmdLine;
        }
    }
}
