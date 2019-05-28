using System.Collections.Generic;
using System.Runtime.Serialization;
using Panacea.Core;
namespace Panacea.Modules.CardActions.Models
{
    [DataContract]
    public class GetActionsResponse
    {
        [DataMember(Name = "cardCategories")]
        public CardCategoriesObject CardCategoriesObject { get; set; }
        [DataMember(Name = "user")]
        public IUser User { get; set; }
    }
    [DataContract]
    public class CardCategoriesObject
    {
        [DataMember(Name = "CardActions")]
        public CardCategoriesHolder CardActions;
    }
    [DataContract]
    public class CardCategoriesHolder
    {
        [DataMember(Name = "cardCategories")]
        public List<CardActionsHolder> CardCategories { get; set; }
    }
    [DataContract]
    public class CardActionsHolder
    {
        [DataMember(Name = "actions")]
        public List<CardActionItem> Actions;
    }
    [DataContract]
    public class CardActionItem
    {
        [DataMember(Name = "action")]
        public CardAction Action;
    }
    [DataContract]
    public class CardAction
    {
        [DataMember(Name = "action")]
        public string Action;
        [DataMember(Name = "settings")]
        public Dictionary<string, dynamic> Settings;
        [DataMember(Name = "forSignedInUser")]
        public bool ForSignedInUser;
    }
}