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
            var parameters = StructuredLogger.CommandLineDiffer.ParseParameters(cmdLine, 0);
            StringBuilder sb = new StringBuilder();
            foreach (var parameter in parameters)
            {
                sb.AppendLine(parameter);
            }

            textEditorLeft.Text = sb.ToString();
        }

        private void CompareNow_Click(object sender, RoutedEventArgs e)
        {
            string left = textEditorLeft.Text;
            string right = textEditorRight.Text;

            StructuredLogger.CommandLineDiffer.TryCompare(left, right, out var leftRemainder, out var rightRemainder);

            StringBuilder sb = new StringBuilder();

            if (leftRemainder.Count + rightRemainder.Count == 0)
            {
                sb.Append("No difference found.");
            }
            else
            {
                sb.AppendLine("Left:");

                foreach (var line in leftRemainder)
                {
                    sb.AppendLine(line);
                }

                sb.AppendLine("\nRight:");

                foreach (var line in rightRemainder)
                {
                    sb.AppendLine(line);
                }
            }

            textEditorResult.Text = sb.ToString();
        }
    }
}
