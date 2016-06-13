using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Definitions;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Framework.Providers;
using DotNetNuke.Instrumentation;
using Satrabel.HttpModules.Provider;
using ZLDNN.Modules.DNNArticle;
namespace HolonCom.OpenUrlRewriter.DNNArticle
{
    public class DNNArticleUrlRuleProvider : UrlRuleProvider
    {
        private const string PROVIDER_TYPE = "urlRule";
        private const string PROVIDER_NAME = "DNNArticleUrlRuleProvider";
        private readonly ProviderConfiguration _providerConfiguration = ProviderConfiguration.GetProviderConfiguration(PROVIDER_TYPE);
        private readonly bool _includePageName = true;
        private readonly Dictionary<string, int> _extrafieldIds = new Dictionary<string, int>();


        public DNNArticleUrlRuleProvider()
        {
            var objProvider = (Provider)_providerConfiguration.Providers[PROVIDER_NAME];
            if (!String.IsNullOrEmpty(objProvider.Attributes["includePageName"]))
            {
                _includePageName = bool.Parse(objProvider.Attributes["includePageName"]);
            }
        }

        public override List<UrlRule> GetRules(int portalId)
        {
            var rules = new List<UrlRule>();

            var articleController = new DNNArticleController();
            var ctl = new ArticleAssignedCategoriesController();
            var ctlrel = new RelatedArticleController();


            try //sometimes a error occurs. we need to know about it.  //todo log the error
            {
                ArrayList modulesByDefinition;

                if (!Debugger.IsAttached)
                {

                    var catlist = new List<string>();
                    var modlist = new List<int>();

                    //CategoryArticleList modules
                    modulesByDefinition = GetModulesByDefinition(portalId, "CategoryArticleList");
                    foreach (ModuleInfo module in modulesByDefinition.OfType<ModuleInfo>())
                    {
                        if (module.PortalID == portalId && !modlist.Contains(module.ModuleID))
                        {
                            var categories = GetCategories(module);
                            foreach (CategoryInfo category in categories)
                            {
                                var catname = category.ItemID.ToString(CultureInfo.InvariantCulture) + "/" + module.ModuleID.ToString(CultureInfo.InvariantCulture); // + "/" + FullCategoryTitleAsPath(category, module.ModuleID);
                                if (!catlist.Contains(catname))
                                {
                                    AddRule(rules, GetCategoryRule(category, module, rules));
                                    catlist.Add(catname);
                                }
                            }
                            modlist.Add(module.ModuleID);
                        }
                    }
                }

                //Gather information of DNNArticleSearch List modules and exclude them from processing
                //because the display all article 
                List<int> lstSeachtModules = new List<int>();
                modulesByDefinition = GetModulesByDefinition(portalId, "DNNArticleSearch");
                foreach (ModuleInfo module in modulesByDefinition.OfType<ModuleInfo>())
                {
                    if (ModuleSetting("ShowInDNNArticleList", module, false) == true)
                        lstSeachtModules.Add(ModuleSetting("DNNArticleListTab", module, -1));
                }


                List<string> urlTemplates = LoadConfig(portalId);

                // DNNA LIST
                // For each DNNA list, load all articles in the defined view
                modulesByDefinition = GetModulesByDefinition(portalId, "DNNArticleList");
                var listModules = modulesByDefinition.OfType<ModuleInfo>();
                foreach (ModuleInfo listModule in listModules)
                {
                    if (lstSeachtModules.Contains(listModule.TabID))
                        continue;
                    var repos = new ArticleListRepository();
                    var viewmoduleTabid = ModuleSetting("ViewTab", listModule, -1);
                    if (viewmoduleTabid == -1)
                    {
                        //DnnLog.Error(string.Format("DNNArticle module with id [{0}] ", listModule.ModuleID));
                        //DnnLog.Error(string.Format("on Portal [{0}]", portalId));
                        //DnnLog.Error(string.Format("tab [{0}] ", listModule.TabID));
                        //DnnLog.Error(string.Format(" with name [{0}] has no View Module defined", listModule.ContentTitle));
                        DnnLog.Error(string.Format("DNNArticle module with id [{0}] on Portal [{3}], tab [{1}] with name [{2}] has no View Module defined", listModule.ModuleID, listModule.TabID, listModule.ContentTitle, portalId));
                    }
                    else
                    {
                        List<DNNArticleInfo> lst = repos.GetArticles(listModule, portalId);
                        foreach (var article in lst)
                        {
                            AddRule(rules, GetArticleRule(article, listModule, urlTemplates, rules));
                            foreach (CategoryInfo category in ctl.GetArticleCategoriesByArticleID(article.ItemId))
                            {
                                AddRule(rules, GetCategoryLinkRule(article, category, listModule, rules));
                            }

                            List<DNNArticleInfo> relatedArticles = ctlrel.GetRelatedDNNArticles(article.ItemId);
                            foreach (var relarticle in relatedArticles)
                            {
                                AddRule(rules, GetArticleRule(relarticle, listModule, urlTemplates, rules));

                                List<DNNArticleInfo> relatedArticles2 = ctlrel.GetRelatedDNNArticles(relarticle.ItemId);
                                foreach (var relarticle2 in relatedArticles2)
                                {
                                    AddRule(rules, GetArticleRule(relarticle2, listModule, urlTemplates, rules));
                                }
                            }
                        }
                    }
                }
                // DNNA DYNAMIC LIST


                //Not all articles are accessible via DNNAlist modules or via related articles. Some are maybe just used in html modules.
                //So we will fetch them all and see if we missed some. Those will we displayed in a default viewModule
                modulesByDefinition = GetModulesByDefinition(portalId, "DNNArticle");
                foreach (ModuleInfo sourceModule in modulesByDefinition.OfType<ModuleInfo>())
                {
                    // Analyse top 60000 articles
                    List<DNNArticleInfo> articles = articleController.GetTopArticlesBySQL(portalId, sourceModule.ModuleID.ToString(CultureInfo.InvariantCulture), "", "publishdate desc", 60000, DisplayType.All, ApprovedDisplayType.All, -1, FeaturedDisplayType.All, -1, false, -1, Null.NullDate, Null.NullDate);
                    foreach (DNNArticleInfo article in articles)
                    {
                        var param = "ArticleId=" + article.ItemId;
                        var found = rules.Any(rule => (rule.Parameters == param));
                        if (!found)
                            AddRule(rules, GetArticleRule(article, sourceModule, urlTemplates, rules));
                    }
                }

            }
            catch (Exception ex)
            {
                var a = ex.Message;
                DnnLog.Error(string.Format("Error HolonCom.DNNArticleURLProvider: Portal [{0}] failed with error [{1}]. Stacktrace: [{2}] ", portalId, ex.Message, ex.StackTrace));
            }
            return rules;
        }

        private static ArrayList GetModulesByDefinition(int portalId, string modulename)
        {
            var mc = new ModuleController();
            var dm = DesktopModuleController.GetDesktopModuleByModuleName(modulename, portalId);
            var md = ModuleDefinitionController.GetModuleDefinitionsByDesktopModuleID(dm.DesktopModuleID).Values.First();

            ArrayList modules = mc.GetModulesByDefinition(portalId, md.FriendlyName);
            return modules;
        }

        private void AddRule(List<UrlRule> rules, UrlRule newrule)
        {
            if (newrule == null)
                return;
            //var found = _rules.Any(rule => (articleRule.Url == rule.Url && articleRule.TabId == rule.TabId && articleRule.Parameters == rule.Parameters));
            var found = rules.Any(rule => (newrule.Url == rule.Url && newrule.TabId == rule.TabId && newrule.Parameters == rule.Parameters));
            if (!found)
                rules.Add(newrule);
        }

        private UrlRule GetArticleRule(DNNArticleInfo article, ModuleInfo sourcemodule, List<string> urlTemplates, List<UrlRule> rules)
        {
            var viewmoduleTabid = int.Parse(sourcemodule.ModuleSettings["ViewTab"].ToString());
            var urlcandidate = "";

            ////do we already have a rule for this article on this tab?
            if (rules.Any(urlRule => (urlRule.TabId == viewmoduleTabid && urlRule.Parameters == "ArticleId=" + article.ItemId)))
                return null;

            var a = "";
            if (article.ItemId == 1379 || article.ItemId == 1480)
                a = "stop";

            Dictionary<string, string> fieldlist = LoadTokenValues(urlTemplates, article);

            //try different template suggestions to find a valid and unique friendlyUrl
            foreach (string urlTemplate in urlTemplates)
            {
                if (MakeUrl(out urlcandidate, viewmoduleTabid, urlTemplate, fieldlist, rules))
                    break;
            }

            //if no unique Url can be created then we don't make a new rule
            if (string.IsNullOrEmpty(urlcandidate))
                return null;

            var rule = new UrlRule
            {
                CultureCode = sourcemodule.CultureCode,
                TabId = viewmoduleTabid, // module.TabID,
                RuleType = UrlRuleType.Module,
                Parameters = "ArticleId=" + article.ItemId,
                Action = UrlRuleAction.Rewrite,
                Url = urlcandidate,
                RemoveTab = !_includePageName
            };

            return rule;
        }

        private Dictionary<string, string> LoadTokenValues(List<string> urlTemplates, DNNArticleInfo article)
        {
            var fieldlist = new Dictionary<string, string>();
            const string pattern = @"\[(.*?)\]";
            var query = String.Join("", urlTemplates.ToArray());
            var matches = Regex.Matches(query, pattern);

            foreach (Match m in matches)
            {
                var token = m.Groups[1].ToString();

                switch (token)
                {
                    case "ITEMID":
                        AddRegularFieldValue("ITEMID", article.ItemId.ToString(CultureInfo.InvariantCulture), fieldlist, urlTemplates);
                        break;
                    case "TITLE":
                        AddRegularFieldValue("TITLE", article.Title, fieldlist, urlTemplates);
                        break;
                    case "SEOTITLE":
                        AddRegularFieldValue("SEOTITLE", article.SEOTitle, fieldlist, urlTemplates);
                        break;
                    case "PUBLISHDATE":
                        AddRegularFieldValue("PUBLISHDATE", article.PublishDate.ToShortDateString(), fieldlist, urlTemplates);
                        break;
                    case "PUBLISHYEAR":
                        AddRegularFieldValue("PUBLISHYEAR", article.PublishDate.Year.ToString(CultureInfo.InvariantCulture), fieldlist, urlTemplates);
                        break;
                    case "USERNAME":
                        AddRegularFieldValue("USERNAME", article.CreatedByUserName, fieldlist, urlTemplates);
                        break;
                    default:
                        //non-regular field. Maybe a ExtraField
                        AddRegularFieldValue(token, GetExtraFieldValue(article.ItemId, article.ModuleId, token), fieldlist, urlTemplates);

                        //if (!fieldlist.ContainsKey(token))
                        //{
                        //    var value = GetExtraFieldValue(article.ItemId, moduleId, token);
                        //    fieldlist.Add(token, value);
                        //}
                        break;
                }
            }

            return fieldlist;
        }

        private void AddRegularFieldValue(string token, string value, Dictionary<string, string> fieldlist, List<string> urlTemplates)
        {
            value = value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                var queue = new Queue<Action>();
                foreach (var urlTemplate in urlTemplates)
                {
                    if (urlTemplate.Contains(token))
                    {
                        var todelete = urlTemplate;
                        queue.Enqueue(() => urlTemplates.Remove(todelete));
                    }
                }
                foreach (var action in queue)
                    action(); // Here we can safely remove the elements as desired.
            }
            else
            {
                if (!fieldlist.ContainsKey(token))
                    fieldlist.Add(token, value);
            }
        }


        private string GetExtraFieldValue(int articleId, int articleModuleId, string tagname)
        {
            //Determine ID of ExtraField
            var extraFieldId = -1;
            if (!_extrafieldIds.ContainsKey(tagname + articleModuleId))
            {
                var lst = new ExtraFieldController().GetByModules(articleModuleId);
                foreach (ExtraFieldInfo o in lst.Where(o => o.Tag == tagname))
                {
                    extraFieldId = o.ItemID;
                    break;
                }
                _extrafieldIds.Add(tagname + articleModuleId, extraFieldId);
            }

            extraFieldId = _extrafieldIds[tagname + articleModuleId];
            if (extraFieldId != -1)
            {
                //Retrieve ExtraField value
                var oFieldValue = new FieldValueController().GetFieldValue(extraFieldId, articleId);
                if (oFieldValue != null)
                    return oFieldValue.FieldValue;
            }

            return "";
        }

        private bool MakeUrl(out string urlcandidate, int tabid, string template, Dictionary<string, string> fieldlist, List<UrlRule> rules)
        {
            urlcandidate = "";
            var newurl = CleanupUrl(ReplaceTokens(template.Replace("/", "tmpslash"), fieldlist)).Replace("tmpslash", "/");

            var a = newurl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = CleanupUrl(a[i].Trim()).Trim('-');
            }
            newurl = string.Join("/", a).Trim('/');
            if (string.IsNullOrEmpty(newurl.Replace("/", "")))
                return false;
            urlcandidate = newurl;

            bool duplicate = rules.Any(urlRule => (urlRule.Url == newurl && urlRule.TabId == tabid));
            if (duplicate)
            {
                urlcandidate = "";
                return false;
            }
            return true;
        }

        static string ReplaceTokens(string template, Dictionary<string, string> replacements)
        {
            var rex = new Regex(@"\[(.*?)\]");
            return (rex.Replace(template, delegate(Match m)
            {
                string key = m.Groups[1].Value;
                string rep = replacements.ContainsKey(key) ? replacements[key] : m.Value;
                return (rep);
            }));
        }

        private UrlRule GetCategoryRule(CategoryInfo category, ModuleInfo module, List<UrlRule> rules)
        {
            object selectedArticleList = ModuleSetting(MySettings.SelectedArticleList, module, category.ModuleID); //List module to use to show the results
            if (selectedArticleList.ToString() == "-1")
                return null;
            object selectedModule = ModuleSetting(MySettings.SelectedModule, module, -1); //base DNNArticle module
            string param = string.Format("cid={0}&smid={1}&tmid={2}", category.ItemID, selectedArticleList, selectedModule);
            bool duplicate = rules.Any(urlRule => (urlRule.Parameters.StartsWith(param)) && urlRule.TabId == module.TabID);
            UrlRule rule = null;
            if (!duplicate)
            {
                rule = new UrlRule
                           {
                               CultureCode = module.CultureCode,
                               TabId = module.TabID,
                               RuleType = UrlRuleType.Module,
                               Parameters = string.Format("cid={0}&smid={1}&tmid={2}", category.ItemID, selectedArticleList, selectedModule),
                               Action = UrlRuleAction.Rewrite,
                               Url = FullCategoryTitleAsPath(category, module.ModuleID),
                               RemoveTab = !_includePageName
                           };
            }

            return rule;
        }

        private static string FullCategoryTitleAsPath(CategoryInfo category, int moduleId)
        {

            string retval = CleanupUrl(category.Title);
            var cc = new CategoryController();
            var parentCat = cc.Get(category.ItemID, moduleId);
            while (parentCat.ParentID != Null.NullInteger)
            {
                parentCat = cc.Get(parentCat.ParentID, moduleId);
                retval = CleanupUrl(parentCat.Title) + "/" + retval;
            }
            return retval;
        }

        private UrlRule GetCategoryLinkRule(DNNArticleInfo article, CategoryInfo category, ModuleInfo module, List<UrlRule> rules)
        {
            object selectedArticleList = ModuleSetting(MySettings.SelectedArticleList, module, -1); //List module to use to show the results
            if (selectedArticleList.ToString() == "-1")
                return null;
            object selectedModule = ModuleSetting(MySettings.SelectedModule, module, -1); //base DNNArticle module
            string param = string.Format("cid={0}&smid={1}&tmid={2}", category.ItemID, selectedArticleList, selectedModule);
            bool duplicate = rules.Any(urlRule => (urlRule.Parameters.StartsWith(param)) && urlRule.TabId == module.TabID);

            UrlRule rule = null;
            if (!duplicate)
            {
                rule = new UrlRule
                            {
                                CultureCode = module.CultureCode,
                                TabId = module.TabID,
                                RuleType = UrlRuleType.Module,
                                Parameters = param,
                                Action = UrlRuleAction.Rewrite,
                                Url = CleanupUrl(category.Title),
                                RemoveTab = !_includePageName
                            };
                rule.Url = FullCategoryTitleAsPath(category, module.ModuleID);
            }

            return rule;
        }

        private ArrayList GetCategories(ModuleInfo module)
        {
            //var ctl = new CategoryController();
            var s = new ArrayList();

            if (ShowExistingCatgory(module) && !string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings[MySettings.SelectedCategory])))
            {
                //string scc = "," + Convert.ToString(module.ModuleSettings[MySettings.SelectedCategory]) + ",";
                string scc = Convert.ToString(module.ModuleSettings[MySettings.SelectedCategory]);
                IEnumerable<CategoryInfo> subCategories = GetCategoriesFromList(scc);

                foreach (CategoryInfo c in subCategories)
                {
                    if (c != null)
                        s.Add(c);
                }
            }
            return s;
        }

        private int ModuleSetting(string settingName, ModuleInfo module, int defaultValue)
        {
            int retValue;
            if (!string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings[settingName])))
                retValue = Convert.ToInt32(module.ModuleSettings[settingName]);
            else
                retValue = defaultValue;
            return retValue;
        }
        private bool ModuleSetting(string settingName, ModuleInfo module, bool defaultValue)
        {
            bool retValue;
            if (!string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings[settingName])))
                retValue = Convert.ToBoolean(module.ModuleSettings[settingName]);
            else
                retValue = defaultValue;
            return retValue;
        }


        private bool ShowExistingCatgory(ModuleInfo module)
        {
            {
                if (!string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings[MySettings.ShowExistingCategories])))
                {
                    return bool.Parse(Convert.ToString(module.ModuleSettings[MySettings.ShowExistingCategories]));
                }
                return false;
            }
        }

        public bool ShowTopLevelOnly(ModuleInfo module)
        {
            {
                if (!string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings["ShowTopLevelOnly"])))
                {
                    return bool.Parse(Convert.ToString(module.ModuleSettings["ShowTopLevelOnly"]));
                }
                return false;
            }
        }

        public bool ShowHasArticleOnly(ModuleInfo module)
        {
            {
                if (!string.IsNullOrEmpty(Convert.ToString(module.ModuleSettings["ShowHasArticleOnly"])))
                {
                    return bool.Parse(Convert.ToString(module.ModuleSettings["ShowHasArticleOnly"]));
                }
                return true;
            }
        }

        private IEnumerable<CategoryInfo> GetCategoriesFromList(string strcategories)
        {
            var categories = new List<CategoryInfo>();
            var ctl = new CategoryController();

            if (!string.IsNullOrEmpty(strcategories))
            {
                string[] cs = strcategories.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in cs)
                {
                    var c = ctl.Get(int.Parse(s), -1);
                    categories.Add(c);
                }
            }
            return categories;
        }

        private List<string> LoadConfig(int portalId)
        {
            var filename = ConfigFile(portalId);
            var urlTemplates = new List<string>(File.ReadAllLines(filename));
            var queue = new Queue<Action>();
            for (int i = 0; i < urlTemplates.Count; i++)
            {
                urlTemplates[i] = urlTemplates[i].Trim();
                if (urlTemplates[i].StartsWith("/") || string.IsNullOrEmpty(urlTemplates[i]))
                {
                    var todelete = urlTemplates[i];
                    queue.Enqueue(() => urlTemplates.Remove(todelete));
                }
            }
            foreach (var action in queue)
                action(); // Here we can safely remove the elements as desired.

            return urlTemplates;
        }

        private string ConfigFile(int portalId)
        {
            PortalSettings _portalSettings = new PortalSettings(portalId);

            var filename = _portalSettings.HomeDirectoryMapPath + "Config\\OpenUrlProvider\\" + "DnnArticleUrlTemplates.txt";
            if (!Directory.Exists(_portalSettings.HomeDirectoryMapPath + "Config\\OpenUrlProvider\\"))
                Directory.CreateDirectory(_portalSettings.HomeDirectoryMapPath + "Config\\OpenUrlProvider\\");
            if (!File.Exists(filename))
            {
                StreamWriter objWriteStream = File.CreateText(filename);
                objWriteStream.Write(@"//Define 1 url template per line
//The first template that resolves to a valid and unique url will be used. 
//Use any valid token: [TITLE], [ITEMID], [SEOTITLE], [PUBLISHDATE], [PUBLISHYEAR], [USERNAME] or any ExtraField Token
//Consider to keep following default definitions in place as fall back templates
[SEOTITLE]
[TITLE]
[ITEMID]/[SEOTITLE]
[ITEMID]/[TITLE]");
                objWriteStream.Close();
            }

            return filename;
        }



    }

}
