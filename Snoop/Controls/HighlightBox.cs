using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Snoop.Controls
{
    public class HighlightBox : FrameworkElement
    {
        public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register("Highlight", typeof(string), typeof(HighlightBox), new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((HighlightBox) d).OnHighlightChanged((string) e.OldValue, (string) e.NewValue)));
        string highlightLower;
        string textLower;
        protected internal virtual void OnHighlightChanged(string oldValue, string newValue) {
            highlightLower = newValue?.ToLower();
            CheckInvalidateHighlight();
        }

        void CheckInvalidateHighlight() {
            if ((string.IsNullOrEmpty(textLower) || string.IsNullOrEmpty(highlightLower)) && containsText) {
                containsText = false;
                InvalidateTextProperties();
                return;
            }
            var newContainsText = !string.IsNullOrEmpty(textLower) && !string.IsNullOrEmpty(highlightLower) && textLower.Contains(highlightLower);
            if (containsText && !newContainsText) {
                InvalidateHighlight();
                InvalidateVisual();
                return;
            }
            containsText = newContainsText;
            if (containsText) {
                InvalidateTextProperties();
                InvalidateVisual();
            }                
        }

//
        public string Highlight { get { return (string) GetValue(HighlightProperty); } set { SetValue(HighlightProperty, value); } }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HighlightBox), new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.AffectsMeasure, (d, e) => ((HighlightBox) d).OnTextChanged((string) e.OldValue, (string) e.NewValue), (o, value) => value ?? ""));

        protected internal virtual void OnTextChanged(string oldValue, string newValue) {
            textLower = newValue?.ToLower();
            InvalidateTextProperties();
            CheckInvalidateHighlight();
        }

        void InvalidateTextProperties() {
            glyphRun = null;
            alignmentBox = null;
            InvalidateHighlight();
        }

        void InvalidateHighlight() { highlightDeltas = null; }

//
        public string Text { get { return (string) GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
        GlyphRun glyphRun;
        bool containsText = false;
        double[][] highlightDeltas;
        Rect? alignmentBox;
        Rect AlignmentBox { get { return (Rect) (alignmentBox ?? (alignmentBox = GlyphRun.ComputeAlignmentBox())); } }

        GlyphRun GlyphRun {
            get { return glyphRun ?? (glyphRun = CreateGlyphRun()); }
        }

        GlyphRun CreateGlyphRun() {            
            var typeface = new Typeface(TextBlock.GetFontFamily(this), TextBlock.GetFontStyle(this), TextBlock.GetFontWeight(this), TextBlock.GetFontStretch(this));
            GlyphTypeface glyphTypeface;
            if (!typeface.TryGetGlyphTypeface(out glyphTypeface))
                throw new InvalidOperationException("No glyphtypeface found");            
            string text = Text;            
            List<int> highlightIndices = new List<int>();
            if (containsText) {
                var index = 0;
                while (index != -1) {
                    index = textLower.IndexOf(highlightLower, index);
                    if (index != -1) {
                        highlightIndices.Add(index);
                        index += highlightLower.Length;
                    }
                }
            }
            double size = TextBlock.GetFontSize(this);

            ushort[] glyphIndexes = new ushort[text?.Length ?? 0];
            double[] advanceWidths = new double[text?.Length ?? 0];

            double totalWidth = 0;
            int currentHighlightIndex = -1;
            int currentHighlightValue = -1;
            if (highlightIndices.Count > 0)
                highlightDeltas = new double[highlightIndices.Count][];
            for (int n = 0; n < (text?.Length ?? 0); n++) {
                if (highlightIndices.Contains(n)) {                    
                    currentHighlightIndex++;
                    highlightDeltas[currentHighlightIndex] = new double[2];
                    currentHighlightValue = n;
                    highlightDeltas[currentHighlightIndex][0] = totalWidth;
                }
                if (currentHighlightValue != -1 && n == currentHighlightValue + highlightLower.Length) {
                    highlightDeltas[currentHighlightIndex][1] = totalWidth;
                    currentHighlightValue = -1;
                }                
                ushort glyphIndex = glyphTypeface.CharacterToGlyphMap[text[n]];
                glyphIndexes[n] = glyphIndex;

                double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
                advanceWidths[n] = width;

                totalWidth += width;                
            }
            if (highlightDeltas != null && Math.Abs(highlightDeltas[highlightDeltas.Length-1][1]) < double.Epsilon) {
                highlightDeltas[highlightDeltas.Length - 1][1] = totalWidth;
            }

            Point origin = new Point();

            return new GlyphRun(glyphTypeface, 0, false, size,
                                glyphIndexes, origin, advanceWidths, null, null, null, null,
                                null, null);
        }

        protected override Size MeasureOverride(Size availableSize) {
            if (String.IsNullOrEmpty(Text))
                return new Size();
            return AlignmentBox.Size;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            if (String.IsNullOrEmpty(Text))
                return new Size();
            return DesiredSize;
        }

        protected override void OnRender(DrawingContext drawingContext) {
            if (String.IsNullOrEmpty(Text))
                return;
            var glyphRun = GlyphRun;
            if (highlightDeltas != null) {
                foreach (double[] delta in highlightDeltas) {
                    drawingContext.DrawRectangle(Brushes.Yellow, null, new Rect(new Point(delta[0], 0), new Point(delta[1], RenderSize.Height)));
                }
            }
            drawingContext.PushTransform(new TranslateTransform(0, -AlignmentBox.Y));
            drawingContext.DrawGlyphRun(TextElement.GetForeground(this), glyphRun);
            drawingContext.Pop();
        }
    }
}
