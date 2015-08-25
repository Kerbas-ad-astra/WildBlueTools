﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using PartListCategories;
using RUI.Icons.Selectable;

/*
Source code copyright 2015, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class ModFilter
    {
        public string modName;

        public bool IsPartInMod(AvailablePart availablePart)
        {
            string[] folderNames = modName.Split(new char[] { ';' });

            if (string.IsNullOrEmpty(availablePart.partUrl))
            {
                UrlDir.UrlConfig url = GameDatabase.Instance.GetConfigs("PART").FirstOrDefault(u => u.name.Replace('_', '.') == availablePart.name);
                if (url == null)
                    return false;

                availablePart.partUrl = url.url;
            }

            foreach (string folderName in folderNames)
                if (availablePart.partUrl.Contains(folderName))
                    return true;

            return false;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ModCategorizer : MonoBehaviour
    {
        private void AddFilterByMod()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("MODCAT");
            Texture2D normalIcon = new Texture2D(32, 32);
            Texture2D selectedIcon = new Texture2D(32, 32);
            string normalPath;
            string selectedPath;
            string folderName;
            string title;
            Color defaultButtonColor = new Color(0, 54, 99);
            char[] delimiters = { ',' };
            Icon categoryIcon;
            PartCategorizer.Category categoryFilter;
            RUIToggleButtonTyped categoryButton;

            foreach (ConfigNode configNode in nodes)
            {
                title = configNode.GetValue("title");
                if (string.IsNullOrEmpty(title))
                    continue;

                folderName = configNode.GetValue("folderName");
                if (string.IsNullOrEmpty(folderName))
                    continue;

                normalPath = configNode.GetValue("normalPath");
                selectedPath = configNode.GetValue("selectedPath");
                if (string.IsNullOrEmpty(normalPath) || string.IsNullOrEmpty(selectedPath))
                    continue;

                normalIcon = GameDatabase.Instance.GetTexture(normalPath, false);
                selectedIcon = GameDatabase.Instance.GetTexture(selectedPath, false);
                if (selectedIcon == null || normalIcon == null)
                    continue;

                ModFilter modFilter = new ModFilter();
                modFilter.modName = folderName;

                categoryIcon = new Icon(folderName + " icon", normalIcon, selectedIcon);
                categoryFilter = PartCategorizer.Instance.filters.Find(f => f.button.categoryName == "Filter by Function");
                PartCategorizer.AddCustomSubcategoryFilter(categoryFilter, title, categoryIcon, p => modFilter.IsPartInMod(p));

                categoryButton = categoryFilter.button.activeButton;
                categoryButton.SetFalse(categoryButton, RUIToggleButtonTyped.ClickType.FORCED);
                categoryButton.SetTrue(categoryButton, RUIToggleButtonTyped.ClickType.FORCED);
            }
        }

        private void Awake()
        {
            GameEvents.onGUIEditorToolbarReady.Add(AddFilterByMod);
        }
    }
}
