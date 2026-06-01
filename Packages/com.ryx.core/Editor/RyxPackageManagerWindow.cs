using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Ryx.Core.Editor
{
    public class RyxPackageManagerWindow : EditorWindow
    {
        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private List<UnityEditor.PackageManager.PackageInfo> _ryxPackages = new List<UnityEditor.PackageManager.PackageInfo>();
        private Dictionary<string, List<string>> _packageTags = new Dictionary<string, List<string>>();
        private UnityEditor.PackageManager.PackageInfo _selectedPackage;

        private Vector2 _sidebarScroll;
        private Vector2 _detailsScroll;
        private string _searchText = "";
        private float _sidebarWidth = 250f;
        private bool _isResizingSidebar;
        private Texture2D _selectedTex;
        private GUIStyle _itemStyle;
        private GUIStyle _selectedItemStyle;
        private GUIStyle _updateIndicatorStyle;
        private bool _isApplyingVersion;
        private string _operationMessage;
        private MessageType _operationMessageType = MessageType.None;

        private const string PendingPackageNameKey = "Ryx.PackageManager.PendingPackageName";
        private const string PendingPackageVersionKey = "Ryx.PackageManager.PendingPackageVersion";

        [MenuItem("Ryx/Package Manager")]
        public static void ShowWindow()
        {
            GetWindow<RyxPackageManagerWindow>("Ryx Package Manager");
        }

        private void OnEnable()
        {
            RefreshPackageList();
            RestorePendingOperation();
        }

        private void EnsureStyles()
        {
            if (_itemStyle != null) return;

            _selectedTex = MakeTex(2, 2, new Color(0.24f, 0.37f, 0.58f));
            _itemStyle = new GUIStyle(EditorStyles.label);
            _itemStyle.padding = new RectOffset(10, 10, 5, 5);

            _selectedItemStyle = new GUIStyle(_itemStyle);
            _selectedItemStyle.normal.background = _selectedTex;
            _selectedItemStyle.normal.textColor = Color.white;

            _updateIndicatorStyle = new GUIStyle(EditorStyles.miniLabel);
            _updateIndicatorStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
            _updateIndicatorStyle.alignment = TextAnchor.MiddleRight;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProgressListRequest;
            EditorApplication.update -= ProgressAddRequest;
        }

        private void RefreshPackageList()
        {
            EditorApplication.update -= ProgressListRequest;
            _listRequest = Client.List(true);
            EditorApplication.update += ProgressListRequest;
        }

        private void ProgressListRequest()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;

            if (_listRequest.Status == StatusCode.Success)
            {
                _ryxPackages = _listRequest.Result
                    .Where(p => p.name.StartsWith("com.ryx"))
                    .OrderBy(p => p.name)
                    .ToList();

                FetchAllTags();

                if (_selectedPackage != null)
                {
                    _selectedPackage = _ryxPackages.FirstOrDefault(p => p.name == _selectedPackage.name);
                }

                ResolvePendingOperation();
            }
            else
            {
                Debug.LogError($"Failed to list packages: {_listRequest.Error.message}");
            }

            EditorApplication.update -= ProgressListRequest;
            Repaint();
        }

        private void FetchAllTags()
        {
            var allTags = GitUtils.GetAllTags();
            _packageTags.Clear();
            foreach (var package in _ryxPackages)
            {
                var tags = allTags
                    .Where(t => t.StartsWith(package.name + "/"))
                    .Select(t => t.Replace(package.name + "/", ""))
                    .OrderByDescending(v => TryParseVersion(v))
                    .ToList();
                _packageTags[package.name] = tags;
            }
        }

        private Version TryParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return new Version(0, 0, 0);
            string cleanVersion = v.Split('-')[0];
            if (Version.TryParse(cleanVersion, out var result)) return result;
            return new Version(0, 0, 0);
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            DrawSidebar();
            DrawResizer();
            DrawDetails();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshPackageList();
            }
            GUILayout.FlexibleSpace();
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchText = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_sidebarWidth));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            var filteredPackages = _ryxPackages
                .Where(p => string.IsNullOrEmpty(_searchText) || p.displayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 || p.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (var package in filteredPackages)
            {
                bool isSelected = _selectedPackage != null && _selectedPackage.name == package.name;
                var style = isSelected ? _selectedItemStyle : _itemStyle;

                Rect rect = EditorGUILayout.BeginHorizontal(style, GUILayout.Height(30));

                string latest = GetLatestVersion(package.name);
                bool hasUpdate = IsNewer(latest, package.version);

                EditorGUILayout.LabelField(package.displayName, isSelected ? _selectedItemStyle : _itemStyle);

                if (hasUpdate)
                {
                    EditorGUILayout.LabelField("●", _updateIndicatorStyle, GUILayout.Width(15));
                }

                EditorGUILayout.EndHorizontal();

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedPackage = package;
                    Repaint();
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawResizer()
        {
            Rect resizerRect = new Rect(_sidebarWidth - 1, 20, 3, position.height - 20);
            EditorGUIUtility.AddCursorRect(resizerRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizerRect.Contains(Event.current.mousePosition))
                _isResizingSidebar = true;

            if (_isResizingSidebar)
            {
                _sidebarWidth = Event.current.mousePosition.x;
                _sidebarWidth = Mathf.Clamp(_sidebarWidth, 100f, position.width - 100f);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
                _isResizingSidebar = false;

            EditorGUI.DrawRect(new Rect(_sidebarWidth, 20, 1, position.height - 20), new Color(0.12f, 0.12f, 0.12f));
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(_operationMessage))
            {
                EditorGUILayout.HelpBox(_operationMessage, _operationMessageType);
                EditorGUILayout.Space(10);
            }

            if (_selectedPackage == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Select a package to see details", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

                // Header
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(_selectedPackage.displayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_selectedPackage.name, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Show in Explorer", GUILayout.Width(120)))
                {
                    EditorUtility.RevealInFinder(_selectedPackage.resolvedPath);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Author: {(_selectedPackage.author != null ? _selectedPackage.author.name : "Ryx Games")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Current Version: {_selectedPackage.version}", EditorStyles.label);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);

                // Description
                EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                string desc = _selectedPackage.description;
                if (string.IsNullOrEmpty(desc)) desc = "No description available for this package.";
                EditorGUILayout.HelpBox(desc, MessageType.None);

                EditorGUILayout.Space(10);

                // Update section
                string latest = GetLatestVersion(_selectedPackage.name);
                if (IsNewer(latest, _selectedPackage.version))
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.HelpBox($"A new version ({latest}) is available!", MessageType.Info);
                    EditorGUI.BeginDisabledGroup(_isApplyingVersion);
                    if (GUILayout.Button($"Update to {latest}", GUILayout.Height(30)))
                    {
                        ApplyVersion(_selectedPackage, latest);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(10);

                // Versions
                if (_packageTags.TryGetValue(_selectedPackage.name, out var tags) && tags.Count > 0)
                {
                    EditorGUILayout.LabelField("Available Versions (from Git tags)", EditorStyles.boldLabel);
                    foreach (var tag in tags)
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        EditorGUILayout.LabelField(tag);
                        if (tag != _selectedPackage.version)
                        {
                            EditorGUI.BeginDisabledGroup(_isApplyingVersion);
                            if (GUILayout.Button("Switch to", GUILayout.Width(80)))
                            {
                                ApplyVersion(_selectedPackage, tag);
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        else
                        {
                            GUI.enabled = false;
                            GUILayout.Button("Installed", GUILayout.Width(80));
                            GUI.enabled = true;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Space(10);

                // Dependencies
                if (_selectedPackage.dependencies != null && _selectedPackage.dependencies.Length > 0)
                {
                    EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
                    foreach (var dep in _selectedPackage.dependencies)
                    {
                        EditorGUILayout.LabelField($"{dep.name} @ {dep.version}", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private string GetLatestVersion(string packageName)
        {
            if (_packageTags.TryGetValue(packageName, out var tags) && tags.Count > 0)
            {
                return tags[0]; // Already sorted descending in FetchAllTags
            }
            return null;
        }

        private bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current)) return false;
            return TryParseVersion(latest) > TryParseVersion(current);
        }

        private void ApplyVersion(UnityEditor.PackageManager.PackageInfo package, string version)
        {
            if (_isApplyingVersion || (_addRequest != null && !_addRequest.IsCompleted)) return;

            string packageUrl = $"{GitUtils.RepositoryUrl}?path=/Packages/{package.name}#{package.name}/{version}";
            SessionState.SetString(PendingPackageNameKey, package.name);
            SessionState.SetString(PendingPackageVersionKey, version);
            _isApplyingVersion = true;
            _operationMessage = $"Installing {package.displayName} {version}...";
            _operationMessageType = MessageType.Info;
            try
            {
                _addRequest = Client.Add(packageUrl);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FailPendingOperation(exception.Message);
                return;
            }

            EditorApplication.update -= ProgressAddRequest;
            EditorApplication.update += ProgressAddRequest;
            Repaint();
        }

        private void ProgressAddRequest()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[Ryx] Updated {_addRequest.Result.name} to {_addRequest.Result.version}");
                CompletePendingOperation(_addRequest.Result.name, _addRequest.Result.version);
                RefreshPackageList();
            }
            else
            {
                string error = _addRequest.Error.message;
                Debug.LogError($"[Ryx] Failed to update package: {error}");
                FailPendingOperation(error);
            }

            EditorApplication.update -= ProgressAddRequest;
            Repaint();
        }

        private void RestorePendingOperation()
        {
            string packageName = SessionState.GetString(PendingPackageNameKey, "");
            string version = SessionState.GetString(PendingPackageVersionKey, "");
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version)) return;

            _isApplyingVersion = true;
            _operationMessage = $"Finishing installation of {packageName} {version}...";
            _operationMessageType = MessageType.Info;
        }

        private void ResolvePendingOperation()
        {
            if (_addRequest != null) return;

            string packageName = SessionState.GetString(PendingPackageNameKey, "");
            string version = SessionState.GetString(PendingPackageVersionKey, "");
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version)) return;

            var package = _ryxPackages.FirstOrDefault(p => p.name == packageName);
            if (package != null && package.version == version)
            {
                CompletePendingOperation(package.displayName, version);
                return;
            }

            FailPendingOperation($"Installed version of {packageName} is not {version}. Check the Unity Console for Package Manager errors.");
        }

        private void CompletePendingOperation(string packageName, string version)
        {
            ClearPendingOperation();
            _operationMessage = $"Installed {packageName} {version}.";
            _operationMessageType = MessageType.Info;
        }

        private void FailPendingOperation(string error)
        {
            ClearPendingOperation();
            _operationMessage = $"Package installation failed: {error}";
            _operationMessageType = MessageType.Error;
        }

        private void ClearPendingOperation()
        {
            SessionState.EraseString(PendingPackageNameKey);
            SessionState.EraseString(PendingPackageVersionKey);
            _isApplyingVersion = false;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
