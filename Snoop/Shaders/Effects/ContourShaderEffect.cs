using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Snoop.Shaders.Effects {
    public class ContourShaderEffect : ShaderEffect {
        static ContourShaderEffect() {
            if (!UriParser.IsKnownScheme("pack"))
                new FlowDocument();
            _pixelShader = new PixelShader() { UriSource = new Uri("pack://application:,,,/Snoop;component/Shaders/Compiled/contourshader.ps", UriKind.RelativeOrAbsolute)};
        }

        static readonly PixelShader _pixelShader;

        public ContourShaderEffect() {
            this.PixelShader = _pixelShader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(SizeProperty); 
        }

        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ContourShaderEffect), 0);
        
        
        public Point Size {
            get { return (Point)GetValue(SizeProperty); }
            set { SetValue(SizeProperty, value); }
        }

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register("Size", typeof(Point), typeof(ContourShaderEffect),
                                        new UIPropertyMetadata(new Point(1,1), PixelShaderConstantCallback(0)));
        
        public Color Selected {
            get { return (Color)GetValue(SelectedProperty); }
            set { SetValue(SelectedProperty, value); }
        }

        public static readonly DependencyProperty SelectedProperty =
            DependencyProperty.Register("Selected", typeof(Color), typeof(ContourShaderEffect),
                                        new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(1)));

        public void SetSelection(Color oldValue, Color? newValue) {
            if (Selected == oldValue || Selected == Colors.Transparent || newValue!=null) {
                Selected = newValue ?? Colors.Transparent;
            }                
        }
       
    }
}