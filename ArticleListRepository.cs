using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using ZLDNN.Modules.DNNArticle;

namespace HolonCom.OpenUrlRewriter.DNNArticle
{
    public class ArticleListRepository
    {
        private Hashtable _settings;
        private int uid;
        private DNNArticleController _ctl = new DNNArticleController();
        private TokenProcessor _ctltoken = new TokenProcessor();
        private int _selectedPortalID;


        #region Public Methods

        public List<DNNArticleInfo> GetArticles(ModuleInfo objModule, int portalId)
        {
            var objModules = new ModuleController();
            _settings = objModules.GetModuleSettings(objModule.ModuleID);
            _selectedPortalID = portalId;
            List<DNNArticleInfo> lst = GetDNNList();

            return lst;

            //var builder = new ArticleRSSBuilder(objModule.ModuleID, PortalSettings.Current, "[TITLE]", "[DESCRIPTION:200]", "[CREATEDBYUSERNAME]", "[PUBLISHDATE:r]"); ;
            //return builder.BuildRSS(lst);
        }

        #endregion


        public void DNNArticleRss()
        {
            uid = Convert.ToInt32(Null.NullInteger);
        }

        public int Recent
        {
            get
            {
                int tn = -1;
                if (!string.IsNullOrEmpty(Convert.ToString(_settings["Recent"])))
                {
                    tn = Convert.ToInt32(_settings["Recent"]);
                }
                return tn;
            }
        }


        private bool MultiModules
        {
            get
            {
                if (!string.IsNullOrEmpty(
                    Convert.ToString(_settings[MySettings.MultiModules])))
                {
                    return Convert.ToBoolean(_settings[MySettings.MultiModules]);
                }
                return false;
            }
        }

        private string SelectedModules
        {
            get
            {
                if (!string.IsNullOrEmpty(
                    Convert.ToString(_settings[MySettings.SelectedModules])))
                {
                    return Convert.ToString(_settings[MySettings.SelectedModules]);
                }
                return "";
            }
        }

        private bool FeaturedOnly
        {
            get
            {
                if (!string.IsNullOrEmpty(
                    Convert.ToString(_settings[MySettings.FeaturedOnly])))
                {
                    return Convert.ToBoolean(_settings[MySettings.FeaturedOnly]);
                }
                return false;
            }
        }

        private bool CheckPermission
        {
            get
            {
                if (!string.IsNullOrEmpty(
                    Convert.ToString(_settings[MySettings.CheckPermission])))
                {
                    return Convert.ToBoolean(_settings[MySettings.CheckPermission]);
                }
                return false;
            }
        }

        public string ViewOrder
        {
            get
            {
                string strDisplayOrder = " CreatedDate Desc";
                if (_settings["OrderField"] != null)
                {
                    strDisplayOrder = " " + Convert.ToString(_settings["OrderField"]) + " ";
                    if (_settings["Order"] != null)
                    {
                        strDisplayOrder += " " + Convert.ToString(_settings["Order"]) + " ";
                    }
                }
                return strDisplayOrder.ToUpper();
            }
        }

        public int SelectedTabID
        {
            get
            {
                int stab = Convert.ToInt32(Null.NullInteger);
                if (!string.IsNullOrEmpty(Convert.ToString(_settings["SelectedTab"])))
                {
                    stab = Convert.ToInt32(_settings["SelectedTab"]);
                }
                return stab;
            }
        }

        public int SelectedModule
        {
            get
            {
                int stab = Convert.ToInt32(Null.NullInteger);
                if (!string.IsNullOrEmpty(Convert.ToString(_settings["SelectedModule"])))
                {
                    stab = Convert.ToInt32(_settings["SelectedModule"]);
                }
                return stab;
            }
        }

        public string SelectedCategory
        {
            get
            {
                string s = Convert.ToString(_settings["SelectedCategory"]);
                if (s != "-1")
                {
                    return s;
                }
                return "";
            }
        }

        public int TopNumber
        {
            get
            {
                int tn = 10;
                if (!string.IsNullOrEmpty(Convert.ToString(_settings["TopNumber"])))
                {
                    tn = Convert.ToInt32(_settings["TopNumber"]);
                }
                return tn;
            }
        }


        private List<DNNArticleInfo> GetDNNList()
        {
            var ctl = new DNNArticleController();
            string modules = "";
            modules = !MultiModules ? SelectedModule.ToString(CultureInfo.InvariantCulture) : SelectedModules;

            int displaynumber = 64000;
            //if (!string.IsNullOrEmpty(Convert.ToString(_settings[MySettings.RssDisplayNumber])))
            //{
            //    displaynumber = int.Parse(Convert.ToString(_settings[MySettings.RssDisplayNumber]));
            //}
            FeaturedDisplayType featured = FeaturedType(_settings);

            var condition = new SearchCondition();
            condition.SearchKey = "";
            condition.portalid = _selectedPortalID;
            condition.modules = modules;
            if (!string.IsNullOrEmpty(SelectedCategory))
            {
                condition.categories.Add(SelectedCategory);
            }

            condition.activeType = DisplayType.All;
            condition.ApprovedType = ApprovedDisplayType.All;
            condition.recent = -1;
            condition.featuredType = featured;
            //condition.CreatedByUserID = uid;
            condition.BeginDate = Null.NullDate;
            condition.EndDate = Null.NullDate;

            // Return ctl.GetTopArticlesBySQL(SelectedPortalID, modules, SelectedCategory, "publishdate desc", displaynumber, DisplayType.ActivedOnly, ApprovedDisplayType.ApprovedOnly, -1, featured, uid, Me.CheckPermission, -1, Null.NullDate, Null.NullDate)
            // return ctl.GetPageArticlesBySQL(condition, "publishdate desc", -1, uid, CheckPermission, 0, displaynumber);

            if (DataCache.GetCache("DNNARSS-" + SelectedModule + "_" + SelectedCategory) != null)
                return (List<DNNArticleInfo>)DataCache.GetCache("DNNARSS-" + SelectedModule + "_" + SelectedCategory);

            var lst = ctl.GetPageArticlesBySQL(condition, "publishdate desc", -1, -1, true, 0, displaynumber);
            //DataCache.SetCache("DNNARSS-" + SelectedModule + "_" + SelectedCategory, lst, TimeSpan.FromDays(1));
            return lst;
        }

        private FeaturedDisplayType FeaturedType(Hashtable Settings)
        {
            if (!string.IsNullOrEmpty(Convert.ToString((Settings[MySettings.FeaturedDisplayType]))))
            {
                return ((FeaturedDisplayType)(Convert.ToInt32(Settings[MySettings.FeaturedDisplayType])));
            }
            if (FeaturedOnly)
            {
                return FeaturedDisplayType.FeatruedOnly;
            }
            return FeaturedDisplayType.All;
        }
    }
}