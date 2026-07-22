using System.Windows;
using System.Windows.Controls;
using Fylo.Models;

namespace Fylo.Converters
{
    public sealed class SidebarTreeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? SectionTemplate { get; set; }
        public DataTemplate? ThisPcTemplate { get; set; }
        public DataTemplate? DriveTemplate { get; set; }
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? RecycleBinTemplate { get; set; }

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
                    SidebarItemType.RecycleBin => RecycleBinTemplate,
                    _ => base.SelectTemplate(item, container)
                };
            }
            return base.SelectTemplate(item, container);
        }
    }
}
