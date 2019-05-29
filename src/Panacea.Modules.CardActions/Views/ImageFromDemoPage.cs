using Panacea.Controls;
using Panacea.Modules.CardActions.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Panacea.Modules.CardActions.Views
{
    public class ImageFromDemoPage : ContentControl
    {
        public ImageFromDemoPage()
        {
            this.Background = Brushes.White;
            this.Loaded += ImageFromDemoPage_Loaded;
        }
        private void ImageFromDemoPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            CacheImage img = new CacheImage() {
                ImageUrl = (DataContext as ImageViewModel).Url,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment =System.Windows.VerticalAlignment.Stretch
            };
            Grid g = new Grid() { Background = Brushes.White };
            g.Children.Add(img);
            Content = g;
        }
    }
}
