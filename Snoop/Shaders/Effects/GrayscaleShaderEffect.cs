using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Snoop.Shaders.Effects {
    public class GrayscaleShaderEffect : ShaderEffect {
        static GrayscaleShaderEffect() {
            if (!UriParser.IsKnownScheme("pack"))
                new FlowDocument();
            _pixelShader = new PixelShader() { UriSource = new Uri("pack://application:,,,/Snoop;component/Shaders/Compiled/grayscaleshader.ps", UriKind.RelativeOrAbsolute)};
        }

        static readonly PixelShader _pixelShader;

        public GrayscaleShaderEffect() {
            this.PixelShader = _pixelShader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(VisibleRectProperty);
        }

        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GrayscaleShaderEffect), 0);
        
        public Point4D VisibleRect {
            get { return (Point4D)GetValue(VisibleRectProperty); }
            set { SetValue(VisibleRectProperty, value); }
        }

        public static readonly DependencyProperty VisibleRectProperty =
            DependencyProperty.Register("VisibleRect", typeof(Point4D), typeof(GrayscaleShaderEffect),
                                        new UIPropertyMetadata(default(Point4D), PixelShaderConstantCallback(0)));
    }
}
