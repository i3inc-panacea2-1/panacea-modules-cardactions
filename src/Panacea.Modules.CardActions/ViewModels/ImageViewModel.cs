using Panacea.Controls;
using Panacea.Core;
using Panacea.Modularity.UiManager;
using Panacea.Modules.CardActions.Views;
using Panacea.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Panacea.Modules.CardActions.ViewModels
{
    [View(typeof(ImageFromDemoPage))]
    public class ImageViewModel : ViewModelBase
    {
        public string Url{ get; set; }
        public ImageViewModel(string url)
        {
            Url = url;
        }
    }
}
