namespace Sitecore.Support.Shell.Applications.ContentEditor.Dialogs.FixHtml
{
    using Sitecore.Configuration;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Layouts;
    using Sitecore.Pipelines;
    using Sitecore.Pipelines.FixXHtml;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls;
    using Sitecore.Web.UI.XamlSharp.Xaml;
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Xml;

    public class FixHtmlPage : XamlMainControl
    {
        protected Scrollbox Fixed;

        protected Literal FixedErrorCount;

        protected Literal FixedErrorCount2;

        protected Scrollbox Original;

        protected Literal OriginalErrorCount;

        protected Literal OriginalErrorCount2;

        protected Memo OriginalMemo;

        protected Memo FixedMemo;

        protected Border ScrollBorder;

        protected Tabstrip TabStrip;

        public string FixedHtml
        {
            get
            {
                return StringUtil.GetString(this.ViewState["FixedHtml"]);
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                this.ViewState["FixedHtml"] = value;
            }
        }

        public string OriginalHtml
        {
            get
            {
                return StringUtil.GetString(this.ViewState["OriginalHtml"]);
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                this.ViewState["OriginalHtml"] = value;
            }
        }

        protected virtual void Cancel_Click()
        {
            SheerResponse.CloseWindow();
        }

        protected virtual void OK_Click()
        {
            string text = this.FixedHtml;
            if (string.IsNullOrEmpty(text))
            {
                text = "__#!$No value$!#__";
            }
            SheerResponse.SetDialogValue(text);
            SheerResponse.CloseWindow();
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            FixHtmlPage.HasAccess();
            base.OnLoad(e);
            if (AjaxScriptManager.Current.IsEvent)
            {
                return;
            }
            UrlHandle urlHandle = UrlHandle.Get();
            string text = StringUtil.GetString(new string[]
            {
                urlHandle["html"]
            });

            this.OriginalHtml = text;

            this.OriginalMemo.Value = text;

            try
            {
                this.Original.InnerHtml = RuntimeHtml.Convert(text, Settings.HtmlEditor.SupportWebControls);
            }
            catch
            {
            }

            FixXHtmlArgs fixXHtmlArgs = new FixXHtmlArgs(this.SanitizeHtml(text));
            using (new LongRunningOperationWatcher(Settings.Profiling.RenderFieldThreshold, "fixXHtml", new string[0]))
            {
                CorePipeline.Run("fixXHtml", fixXHtmlArgs);
            }
            string @string = StringUtil.GetString(new string[]
            {
                fixXHtmlArgs.Html
            });
            this.FixedHtml = @string;
            this.Fixed.InnerHtml = @string;
            this.FixedMemo.Value = @string;
            FixHtmlPage.CountErrors(text, this.OriginalErrorCount);
            FixHtmlPage.CountErrors(@string, this.FixedErrorCount);
            this.OriginalErrorCount2.Text = this.OriginalErrorCount.Text;
            this.FixedErrorCount2.Text = this.FixedErrorCount.Text;
        }

        private string ConvertAmpersands(string input)
        {
            string output = Regex.Replace(input, @"&(?! \w+; | \#[0-9]+; | \#x[0-9A-F]+;)", "&amp;", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            foreach (Match m in Regex.Matches(output, @"&(\w+; | \#[0-9]+; | \#x[0-9A-F]+;)"))
            {
                if (HttpUtility.HtmlDecode(m.Value) == m.Value)
                {
                    output = output.Replace(m.Value, "&amp;" + m.Value.Substring(1));
                }
            }

            return output;
        }

        protected void ClickTab(string id)
        {
            this.TabStrip.SetActive(int.Parse(id.Substring(id.LastIndexOf("_tabdiv") + "_tabdiv".Length)));
        }

        private static void HasAccess()
        {
            Item item = Client.CoreDatabase.GetItem("/sitecore/content/Applications/Content Editor/Dialogs/EditHtml/Ribbon/Home/Write/Fix");
            Item item2 = Client.CoreDatabase.GetItem("/sitecore/system/Field types/Simple Types/Rich Text/Menu/Suggest Fix");
            Assert.HasAccess(item != null && item2 != null && (item.Access.CanRead() || item2.Access.CanRead()), "Access denied");
        }

        protected void ViewFixedErrors()
        {
            FixHtmlPage.ViewErrors(this.FixedHtml);
        }

        protected void ViewOriginalErrors()
        {
            FixHtmlPage.ViewErrors(this.OriginalHtml);
        }

        private static void CountErrors(string html, Literal count)
        {
            Assert.ArgumentNotNull(html, "html");
            Assert.ArgumentNotNull(count, "count");
            html = string.Format("<div>{0}</div>", XHtml.Convert(html));
            html = XHtml.MakeDocument(html, true);
            Collection<XHtmlValidatorError> collection = new XHtmlValidator(html).Validate();
            count.Text = Translate.Text((collection.Count == 1) ? "{0} error" : "{0} errors", new object[]
            {
                collection.Count
            });
        }

        private static void ViewErrors(string html)
        {
            Assert.ArgumentNotNull(html, "html");
            UrlString urlString = new UrlString("/sitecore/shell/~/xaml/Sitecore.Shell.Applications.ContentEditor.Dialogs.EditHtml.ValidateXHtml.aspx");
            UrlHandle expr_1B = new UrlHandle();
            expr_1B["html"] = html;
            expr_1B.Add(urlString);
            SheerResponse.ShowModalDialog(urlString.ToString());
        }

        protected internal string SanitizeHtml(string originalHtml)
        {
            string preparedHtml = originalHtml;
            if (this.ShouldRemoveScripts)
            {
                preparedHtml = this.RemoveAllScripts(originalHtml);
            }
            return this.ConvertAmpersands(preparedHtml);
            //return this.EncodeHtml(preparedHtml);
        }

        protected internal virtual bool ShouldRemoveScripts
        {
            get
            {
                return Settings.HtmlEditor.RemoveScripts;
            }
        }

        protected internal virtual string RemoveAllScripts(string originalHtml)
        {
            return WebUtil.RemoveAllScripts(originalHtml);
        }

        protected internal virtual string EncodeHtml(string preparedHtml)
        {
            return System.Web.HttpUtility.HtmlEncode(preparedHtml);
        }
    }
}