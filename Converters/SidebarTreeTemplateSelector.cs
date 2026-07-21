using System.Windows;
using System.Windows.Controls;
using FastExplorer.Models;

namespace FastExplorer.Converters
{
    public sealed class SidebarTreeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? SectionTemplate { get; set; }
        public DataTemplate? ThisPcTemplate { get; set; }
        public DataTemplate? DriveTemplate { get; set; }
        public DataTemplate? FolderTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is SidebarTreeItem si)
            {
                return si.ItemType switch
                {
                    SidebarItemType.Section => SectionTemplate,
                    SidebarItemType.ThisPc => ThisPcTemplate,
                    SidebarItemType.Drive => DriveTemplate,
                    SidebarItemType.Folder => FolderTemplate,
                    _ => base.SelectTemplate(item, container)
                };
            }
            return base.SelectTemplate(item, container);
        }
    }
}
