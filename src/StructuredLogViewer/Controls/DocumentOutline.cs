using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public class EditorExtension
    {
        public PreprocessedFileManager.PreprocessContext PreprocessContext { get; set; }

        public event Action<Import> ImportSelected;

        public void Install(TextViewerControl textViewerControl)
        {
            textViewerControl.EditorExtension = this;
            var textEditor = textViewerControl.TextEditor;
            var textArea = textEditor.TextArea;
            var caret = textArea.Caret;
            caret.PositionChanged += (s, e) =>
            {
                var caretOffset = textEditor.CaretOffset;
                var projectImport = PreprocessContext.GetImportFromPosition(caretOffset);
                ImportSelected?.Invoke(projectImport);
            };

            textEditor.MouseHover += (sender, e) =>
            {
                if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
                {
                    var mousePos = e.GetPosition(textEditor);
                    this.TryUpdateToolTipText(textViewerControl, mousePos);
                }
            };
        }


        private void TryUpdateToolTipText(TextViewerControl textViewerControl, System.Windows.Point mousePosition)
        {
            var textPos = textViewerControl.TextEditor.GetPositionFromPoint(mousePosition);
            textViewerControl.ToolTip ??= new ToolTip() { Placement = PlacementMode.Relative, PlacementTarget = textViewerControl.TextEditor };
            ToolTip tooltip = textViewerControl.ToolTip as ToolTip;

            if (!textPos.HasValue || !this.GetWordAtPosition(textViewerControl, textPos.Value, out string type, out string word)
                || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(word))
            {
                CloseToolTip();
                return;
            }

            StringBuilder content = new();

            if (type == "<PropertyGroup>")
            {
                var pr = this.PreprocessContext.Evaluation.Children.FirstOrDefault(p => p.Title == "Property reassignment");
                if (pr is TimedNode folder)
                {
                    var pr_word = folder.Children.FirstOrDefault(p => p.Title == word);
                    if (pr_word is Folder word_folder)
                    {
                        foreach (var entry in word_folder.Children)
                        {
                            content.AppendLine(entry.Title);
                        }
                    }
                }

                var propertyGroup = this.PreprocessContext.Evaluation.Children.FirstOrDefault(p => p.Title == "Properties");
                if (propertyGroup is Folder propertyFolder)
                {
                    var propertyEntry = propertyFolder.Children.FirstOrDefault(p => p.Title == word);
                    if (propertyEntry is Property property)
                    {
                        content.Append(property.Value);
                    }
                }
            }

            if (content.Length == 0)
            {
                CloseToolTip();
                return;
            }

            tooltip.HorizontalOffset = mousePosition.X;
            tooltip.VerticalOffset = mousePosition.Y;
            tooltip.Content = $"{type} {word} \n{content}";

            if (!tooltip.IsOpen)
            {
                tooltip.IsOpen = true;
            }

            void CloseToolTip()
            {
                tooltip.Content = string.Empty;
                tooltip.IsOpen = false;
            }
        }

        private bool GetWordAtPosition(TextViewerControl textViewerControl, TextViewPosition textPos, out string type, out string word)
        {
            type = string.Empty;
            word = string.Empty;

            var document = textViewerControl.TextEditor.Document;
            var offset = document.GetOffset(textPos.Line, textPos.Column);
            int start = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(document, offset + 1, System.Windows.Documents.LogicalDirection.Backward, ICSharpCode.AvalonEdit.Document.CaretPositioningMode.WordBorder);
            int end = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(document, offset, System.Windows.Documents.LogicalDirection.Forward, ICSharpCode.AvalonEdit.Document.CaretPositioningMode.WordBorder);

            if (start == -1 || end == -1 || end <= start)
            {
                return false;
            }

            var typeCandiate = textViewerControl.FoldingManager.GetFoldingsContaining(start)?.LastOrDefault(f => allowKeywords.Contains(f.Title));

            if (string.IsNullOrEmpty(typeCandiate?.Title))
            {
                return false;
            }

            word = document.GetText(start, end - start).Trim(specialCharacters).Trim();
            type = typeCandiate.Title;

            if (string.IsNullOrEmpty(word))
            {
                return false;
            }

            return true;
        }

        private char[] specialCharacters = ['@', '$', '(', ')', '<', '>', '\r', '\n', ' '];

        private string[] allowKeywords = [
            "<Project>",
            "<PropertyGroup>",
            "<ItemGroup>",
            ];
    }
}
