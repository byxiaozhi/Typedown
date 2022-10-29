using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class ParagraphState
    {
        public MenuState MenuState { get; set; }

        public ParagraphState(MenuState menuState)
        {
            MenuState = menuState;
            UpdateCheckedMenuItem();
            UpdateEnableMenuItem();
        }

        private void UpdateCheckedMenuItem()
        {
            var affiliation = new HashSet<string>(MenuState.Affiliation.ToObject<JObject>().Properties().Select(x => x.Name));
            TaskList.IsChecked = affiliation.Contains("ul") && MenuState.IsTaskList;
            Table.IsChecked = MenuState.IsTable;
            CodeFences.IsChecked = MenuState.IsCodeFences && affiliation.Where(x => x.Contains("code")).Any();
            Heading1.IsChecked = affiliation.Contains("h1");
            Heading2.IsChecked = affiliation.Contains("h2");
            Heading3.IsChecked = affiliation.Contains("h3");
            Heading4.IsChecked = affiliation.Contains("h4");
            Heading5.IsChecked = affiliation.Contains("h5");
            Heading6.IsChecked = affiliation.Contains("h6");
            Table.IsChecked = MenuState.IsTable;
            HtmlBlock.IsChecked = affiliation.Contains("html");
            MathBlock.IsChecked = affiliation.Contains("multiplemath");
            QuoteBlock.IsChecked = affiliation.Contains("blockquote");
            OrderList.IsChecked = affiliation.Contains("ol");
            BulletList.IsChecked = affiliation.Contains("ul") && !MenuState.IsTaskList;
            Paragraph.IsChecked = affiliation.Contains("p");
            HorizontalLine.IsChecked = affiliation.Contains("hr");
            FrontMatter.IsChecked = affiliation.Contains("frontmatter");
            Chart.IsChecked = (MenuState.IsCodeContent || MenuState.IsCodeFences) && !CodeFences.IsChecked && !MathBlock.IsChecked && !HtmlBlock.IsChecked;
        }

        private void UpdateEnableMenuItem()
        {
            FormatIsEnable = true;
            HyperlinkIsEnable = true;
            ImageIsEnable = true;
            if (MenuState.IsDisabled)
            {
                ResetEnableMenuItem(false);
            }
            else if (MenuState.IsCodeContent || MenuState.IsCodeFences)
            {
                ResetEnableMenuItem(false);
                CodeFences.IsEnable = CodeFences.IsChecked;
            }
            else if (MenuState.IsMultiline)
            {
                ResetEnableMenuItem(true);
                Heading1.IsEnable = false;
                Heading2.IsEnable = false;
                Heading3.IsEnable = false;
                Heading4.IsEnable = false;
                Heading5.IsEnable = false;
                Heading6.IsEnable = false;
                UpgradeHeading.IsEnable = false;
                DegradeHeading.IsEnable = false;
                Table.IsEnable = false;
                HyperlinkIsEnable = false;
                ImageIsEnable = false;
            }
            else if(Heading1.IsChecked || Heading2.IsChecked || Heading3.IsChecked || Heading4.IsChecked || Heading5.IsChecked || Heading6.IsChecked)
            {
                ResetEnableMenuItem(false);
                Heading1.IsEnable = true;
                Heading2.IsEnable = true;
                Heading3.IsEnable = true;
                Heading4.IsEnable = true;
                Heading5.IsEnable = true;
                Heading6.IsEnable = true;
                Paragraph.IsEnable = true;
                UpgradeHeading.IsEnable = !Heading1.IsChecked;
                DegradeHeading.IsEnable = true;
                FormatIsEnable = true;
                HyperlinkIsEnable = true;
            } else
            {
                ResetEnableMenuItem(true);
                DegradeHeading.IsEnable = !Paragraph.IsChecked;
                FormatIsEnable = true;
                HyperlinkIsEnable = true;
                ImageIsEnable = true;
            }
        }

        private void ResetEnableMenuItem(bool IsEnable)
        {
            Heading1.IsEnable = IsEnable;
            Heading2.IsEnable = IsEnable;
            Heading3.IsEnable = IsEnable;
            Heading4.IsEnable = IsEnable;
            Heading5.IsEnable = IsEnable;
            Heading6.IsEnable = IsEnable;
            UpgradeHeading.IsEnable = IsEnable;
            DegradeHeading.IsEnable = IsEnable;
            Paragraph.IsEnable = IsEnable;
            Table.IsEnable = IsEnable;
            CodeFences.IsEnable = IsEnable;
            HtmlBlock.IsEnable = IsEnable;
            MathBlock.IsEnable = IsEnable;
            QuoteBlock.IsEnable = IsEnable;
            OrderList.IsEnable = IsEnable;
            BulletList.IsEnable = IsEnable;
            TaskList.IsEnable = IsEnable;
            HorizontalLine.IsEnable = IsEnable;
            FrontMatter.IsEnable = IsEnable;
            Chart.IsEnable = IsEnable;
        }

        public MenuItemModel Heading1 { get; set; } = new();
        public MenuItemModel Heading2 { get; set; } = new();
        public MenuItemModel Heading3 { get; set; } = new();
        public MenuItemModel Heading4 { get; set; } = new();
        public MenuItemModel Heading5 { get; set; } = new();
        public MenuItemModel Heading6 { get; set; } = new();
        public MenuItemModel UpgradeHeading { get; set; } = new();
        public MenuItemModel DegradeHeading { get; set; } = new();
        public MenuItemModel Paragraph { get; set; } = new();
        public MenuItemModel Table { get; set; } = new();
        public MenuItemModel CodeFences { get; set; } = new();
        public MenuItemModel HtmlBlock { get; set; } = new();
        public MenuItemModel MathBlock { get; set; } = new();
        public MenuItemModel QuoteBlock { get; set; } = new();
        public MenuItemModel OrderList { get; set; } = new();
        public MenuItemModel BulletList { get; set; } = new();
        public MenuItemModel TaskList { get; set; } = new();
        public MenuItemModel HorizontalLine { get; set; } = new();
        public MenuItemModel FrontMatter { get; set; } = new();
        public MenuItemModel Chart { get; set; } = new();

        public bool FormatIsEnable { get; set; }
        public bool HyperlinkIsEnable { get; set; }
        public bool ImageIsEnable { get; set; }
    }
}
