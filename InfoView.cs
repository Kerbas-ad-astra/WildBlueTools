using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace WildBlueIndustries
{
    class InfoView : Window<OpsView>
    {
        public string ModuleInfo;
        public Texture moduleLabel;

        private Vector2 _scrollPos;

        public InfoView() :
        base("Module Info", 320, 400)
        {
            Resizable = false;
            _scrollPos = new Vector2(0, 0);
        }

        protected override void DrawWindowContents(int windowId)
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            if (moduleLabel != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(moduleLabel, new GUILayoutOption[] { GUILayout.Width(128), GUILayout.Height(128) });
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(ModuleInfo);
            GUILayout.EndScrollView();
        }

    }
}
