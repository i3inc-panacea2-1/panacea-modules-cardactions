using Panacea.Controls;
using Panacea.Modules.CardActions.Views;
using Panacea.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Panacea.Modules.CardActions.ViewModels
{
    [View(typeof(Image))]
    public class ImageViewModel : ViewModelBase
    {
        public object Content { get; set; }
        public string Url{ get; set; }
        public ImageViewModel()
        {
        }
        public ImageViewModel(string url)
        {
            Url = url;
            Content = new CacheImage() { ImageUrl = url };
        }
    }
}
