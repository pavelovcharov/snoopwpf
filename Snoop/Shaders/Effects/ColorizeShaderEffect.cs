using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Snoop.Shaders.Effects {
    public class ColorizeShaderEffect : ShaderEffect {
        static ColorizeShaderEffect() {
            if (!UriParser.IsKnownScheme("pack"))
                new FlowDocument();
            _pixelShader = new PixelShader() { UriSource = new Uri("pack://application:,,,/Snoop;component/Shaders/Compiled/colorizeshader.ps", UriKind.RelativeOrAbsolute)};
        }

        static readonly PixelShader _pixelShader;

        public ColorizeShaderEffect() {
            this.PixelShader = _pixelShader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TargetColorProperty);
        }

        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ColorizeShaderEffect), 0);

        public Color TargetColor {
            get { return (Color)GetValue(TargetColorProperty); }
            set { SetValue(TargetColorProperty, value); }
        }

        public static readonly DependencyProperty TargetColorProperty =
            DependencyProperty.Register("TargetColor", typeof(Color), typeof(ColorizeShaderEffect),
              new UIPropertyMetadata(Colors.White, PixelShaderConstantCallback(0)));
    }
}
