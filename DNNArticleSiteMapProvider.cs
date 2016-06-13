using System;
using System.Collections.Generic;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Sitemap;


namespace ZLDNN.Modules.DNNArticle.Components
{
    public class DNNArticleiteMapProvider : SitemapProvider
    {
        private Dictionary<int, float> _modulePriority = new Dictionary<int, float>();
        private Dictionary<int, int> _modulePriorityFieldId = new Dictionary<int, int>();

        public override List<SitemapUrl> GetUrls(int portalId, PortalSettings ps, string version)
        {
            var ctl = new DNNArticleController();

            var lst = ctl.GetTopArticlesBySQL(portalId, "", "", "publishdate desc", 5000,
                                              DisplayType.ActivedOnly, ApprovedDisplayType.ApprovedOnly, -1,
                                              FeaturedDisplayType.All, -1, true, -1, Null.NullDate, Null.NullDate);
            var urls = new List<SitemapUrl>();
            foreach(var dnnArticleInfo in lst)
            {
                var blogUrl = GetBlogUrl(dnnArticleInfo);
                urls.Add(blogUrl);
            }

            return urls;
        }

        private SitemapUrl  GetBlogUrl(DNNArticleInfo article)
        {

            if(!_modulePriority.ContainsKey(article.ModuleId))
            {
                var settings = new ModuleController().GetModuleSettings(article.ModuleId);
                if(!string.IsNullOrEmpty(Convert.ToString(settings[MySettings.SiteMapPriority])))
                {
                    try
                    {
                        _modulePriority.Add(article.ModuleId, Convert.ToSingle(settings[MySettings.SiteMapPriority]));
                    }
                    catch (Exception)
                    {

                        _modulePriority.Add(article.ModuleId, 0.5f);
                    }
                   
                }
                else
                {
                    _modulePriority.Add(article.ModuleId, 0.5f);
                }

                if (!string.IsNullOrEmpty(Convert.ToString(settings[MySettings.SiteMapPriorityFieldId])))
                {
                    _modulePriorityFieldId.Add(article.ModuleId, Convert.ToInt32(settings[MySettings.SiteMapPriorityFieldId]));
                }
                else
                {
                    _modulePriorityFieldId.Add(article.ModuleId, -1);
                }
            }

            float priority = _modulePriority[article.ModuleId];
            int fieldid = _modulePriorityFieldId[article.ModuleId];

            if(!Null.IsNull(fieldid))
            {
                var o = new FieldValueController().GetFieldValue(fieldid, article.ItemId);
                if (o != null)
                {
                    var prio = o.GetValue();
                    if (!string.IsNullOrEmpty(prio))
                    {
                        priority = Convert.ToSingle(prio);
                    }
                }
            }
            var pageUrl = new SitemapUrl
                              {
                                  Url =
                                      Modules.DNNArticle.TokenProcessor.GetViewURL(article, Null.NullInteger,
                                                                                         Null.NullInteger),
                                  Priority = priority,
                                  LastModified = article.PublishDate,
                                  ChangeFrequency = SitemapChangeFrequency.Weekly
                              };

            return pageUrl;
        }
    }
}