﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static System.Linq.Expressions.Expression;
using Object = UnityEngine.Object;

namespace AV.Hierarchy
{
    internal class SmartHierarchy
    {
        internal static HierarchyPreferences preferences => HierarchySettingsProvider.Preferences;
        internal static Event evt => Event.current;
        internal static SmartHierarchy lastHierarchy;
        
        internal SceneHierarchyWindow window { get; }
        internal SceneHierarchy hierarchy => window.hierarchy;
        internal TreeViewState state => hierarchy.state;
        internal float time => Time.realtimeSinceStartup;
        
        private EditorWindow actualWindow => window.actualWindow;
        
        private readonly VisualElement root;
        private readonly HoverPreview hoverPreview;
        private readonly Dictionary<int, ViewItem> ItemsData = new Dictionary<int, ViewItem>();
       
        
        public SmartHierarchy(EditorWindow window)
        {
            root = window.rootVisualElement;
            this.window = new SceneHierarchyWindow(window);
            
            hoverPreview = new HoverPreview { hierarchy = this };
            
            RegisterCallbacks();
            hierarchy.ReassignCallbacks();
            
            var rootGuiContainer = root.parent.Query<IMGUIContainer>().First();
            rootGuiContainer.onGUIHandler  += OnGUI;
            
            root.Add(hoverPreview);

            window.SetAntiAliasing(8);
        }
        
        [InitializeOnLoadMethod]
        private static void OnInitialize()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
        }

        private void RegisterCallbacks()
        {
            HierarchySettingsProvider.onChange += ReloadView;
            HierarchySettingsProvider.onChange += ImmediateRepaint;
            
            Selection.selectionChanged += ReloadView;
            hierarchy.onVisibleRowsChanged += ReloadView;
            EditorApplication.hierarchyChanged += ReloadView;
        }
        
        private void ReloadView()
        {
            ItemsData.Clear();
        }
        
        private static void ImmediateRepaint()
        {
            EditorApplication.DirtyHierarchyWindowSorting();
        }
        
        private void OnGUI()
        {
            if (!preferences.enableSmartHierarchy)
                return;

            hierarchy.EnsureValidData();

            if (preferences.enableHoverPreview)
            {
                var isMagnifyHold = false;
                switch (preferences.magnifyHoldKey)
                {
                    case ModificationKey.Alt: isMagnifyHold = evt.alt; break;
                    case ModificationKey.Shift: isMagnifyHold = evt.shift; break;
                    case ModificationKey.Control: isMagnifyHold = evt.control; break;
                }
                var showPreview = isMagnifyHold || preferences.alwaysShowPreview;
                
                if (showPreview && hierarchy.hoveredItem != null)
                {
                    var isHovering = ItemsData.TryGetValue(hierarchy.hoveredItem.id, out var item);
                    var hasPreview = true;

                    if (isHovering)
                        hoverPreview.OnItemPreview(item);
                }
                else
                {
                    hoverPreview.Hide();
                }
                
                hoverPreview.UpdatePosition();
            }
        }

        private static void OnHierarchyItemGUI(int instanceId, Rect rect)
        {
            if (!preferences.enableSmartHierarchy)
                return;
            
            lastHierarchy = HierarchyInitialization.GetLastHierarchy();
            lastHierarchy.OnItemGUI(instanceId, rect);
        }
        
        private void OnItemGUI(int id, Rect rect)
        {
            var instance = EditorUtility.InstanceIDToObject(id) as GameObject;

            if (!instance)
                return;
            
            GetInstanceViewItem(id, instance, rect, out var item);
            
            // Happens to be null when entering prefab mode
            if (!item.EnsureViewExist(hierarchy))
                return;

            var fullWidthRect = GetFullWidthRect(rect);
            
            item.UpdateViewIcon();

            if (hierarchy.hoveredItem == item.view)
            {
                OnHoverGUI(fullWidthRect, item);
            }
        }
        
        private void GetInstanceViewItem(int id, GameObject instance, Rect rect, out ViewItem item)
        {
            if (!ItemsData.TryGetValue(id, out item))
            {
                item = new ViewItem(instance) { rect = rect };

                ItemsData.Add(id, item);
            }
        }

        private void OnHoverGUI(Rect rect, ViewItem item)
        {
            var instance = item.instance;
            
            var toggleRect = new Rect(rect) { x = 32 };
            if (OnLeftToggle(toggleRect, instance.activeSelf, out var isActive))
            {
                Undo.RecordObject(instance, "GameObject Set Active");
                instance.SetActive(isActive);
            }
        }

        private static Rect GetFullWidthRect(Rect rect)
        {
            var fullWidthRect = new Rect(rect) { x = 0, width = Screen.width };
            return fullWidthRect;
        }

        private static bool OnLeftToggle(Rect rect, bool isActive, out bool value)
        {
            var toggleRect = new Rect(rect) { width = 16 };
            
            EditorGUI.BeginChangeCheck();
            value = GUI.Toggle(toggleRect, isActive, GUIContent.none);
            return EditorGUI.EndChangeCheck();
        }
    }
}