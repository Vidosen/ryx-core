using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ryx.Core.Editor
{
    public class RyxPackageManagerWindow : EditorWindow
    {
        [Serializable]
        private class PackageManifestAuthor
        {
            public string name;
        }

        [Serializable]
        private class PackageManifestData
        {
            public string name;
            public string displayName;
            public string description;
            public PackageManifestAuthor author;
        }

        private sealed class PackageVersionInfo
        {
            public string Version;
            public string GitRef;
        }

        private sealed class PackageCatalogEntry
        {
            public string Name;
            public string DisplayName;
            public string Description;
            public string AuthorName;
            public List<PackageVersionInfo> AvailableVersions = new List<PackageVersionInfo>();
            public UnityEditor.PackageManager.PackageInfo InstalledPackage;

            public string EffectiveDisplayName => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
            public string EffectiveDescription => string.IsNullOrEmpty(Description) ? "No description available for this package." : Description;
            public string EffectiveAuthorName => string.IsNullOrEmpty(AuthorName) ? "Ryx Games" : AuthorName;
            public bool IsInstalled => InstalledPackage != null;
            public string InstalledVersion => InstalledPackage?.version;
            public string LatestVersion => AvailableVersions.Count > 0 ? AvailableVersions[0].Version : null;
            public bool HasRemoteVersions => AvailableVersions.Count > 0;
            public bool HasUpdate(Func<string, string, bool> isNewer) => IsInstalled && isNewer(LatestVersion, InstalledVersion);
        }

        private sealed class PackageListItemElements
        {
            public VisualElement Root;
            public Label Title;
            public Label PackageId;
            public Label StatusChip;
        }

        private sealed class VersionRowElements
        {
            public Label VersionLabel;
            public Button ActionButton;
        }

        private const string WindowAssetPath = "Packages/com.ryx.core/Editor/UI/RyxPackageManagerWindow.uxml";
        private const string WindowStylePath = "Packages/com.ryx.core/Editor/UI/RyxPackageManagerWindow.uss";
        private const string PendingPackageNameKey = "Ryx.PackageManager.PendingPackageName";
        private const string PendingPackageVersionKey = "Ryx.PackageManager.PendingPackageVersion";

        private const string StatusInstalledClass = "status-chip--installed";
        private const string StatusNotInstalledClass = "status-chip--not-installed";
        private const string StatusUpdateClass = "status-chip--update";
        private const string BannerInfoClass = "details-card--info";
        private const string BannerWarningClass = "details-card--warning";
        private const string BannerErrorClass = "details-card--error";

        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private readonly List<PackageCatalogEntry> _packages = new List<PackageCatalogEntry>();
        private readonly List<PackageCatalogEntry> _filteredPackages = new List<PackageCatalogEntry>();
        private PackageCatalogEntry _selectedPackage;

        private bool _isCatalogLoading;
        private bool _isApplyingVersion;
        private string _operationMessage;
        private MessageType _operationMessageType = MessageType.None;
        private string _pendingPackageUrl;

        private ListView _packageListView;
        private VisualElement _catalogLoadingState;
        private VisualElement _catalogEmptyState;
        private Label _catalogEmptyTitleLabel;
        private Label _catalogEmptyBodyLabel;
        private ToolbarSearchField _searchField;
        private Button _refreshButton;
        private Label _catalogCountLabel;
        private VisualElement _detailsRoot;
        private VisualElement _emptyState;
        private VisualElement _headerCard;
        private Label _headerTitleLabel;
        private Label _headerPackageIdLabel;
        private Label _headerStatusChip;
        private Button _showInExplorerButton;
        private Label _authorValueLabel;
        private Label _currentVersionValueLabel;
        private Label _latestVersionValueLabel;
        private Label _descriptionValueLabel;
        private HelpBox _operationBanner;
        private VisualElement _primaryActionCard;
        private HelpBox _primaryActionMessage;
        private Label _primaryActionSummaryLabel;
        private Button _primaryActionButton;
        private VisualElement _versionsSection;
        private ScrollView _versionsList;
        private VisualElement _dependenciesSection;
        private ScrollView _dependenciesList;

        [MenuItem("Ryx/Package Manager")]
        public static void ShowWindow()
        {
            GetWindow<RyxPackageManagerWindow>("Ryx Package Manager");
        }

        private void OnEnable()
        {
            RestorePendingOperation();
            RefreshPackageList();
            RefreshViewState();
        }

        private void CreateGUI()
        {
            BuildVisualTree();
            WireUi();
            RefreshListView();
            RefreshViewState();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProgressListRequest;
            EditorApplication.update -= ProgressAddRequest;
            if (!string.IsNullOrEmpty(_pendingPackageUrl))
            {
                _pendingPackageUrl = null;
                ClearPendingOperation();
            }
        }

        private void BuildVisualTree()
        {
            rootVisualElement.Clear();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WindowAssetPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(WindowStylePath);

            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox($"Missing UI asset: {WindowAssetPath}", HelpBoxMessageType.Error));
                return;
            }

            tree.CloneTree(rootVisualElement);
            if (style != null)
            {
                rootVisualElement.styleSheets.Add(style);
            }

            rootVisualElement.AddToClassList("package-manager");
        }

        private void WireUi()
        {
            _refreshButton = rootVisualElement.Q<Button>("refresh-button");
            _searchField = rootVisualElement.Q<ToolbarSearchField>("search-field");
            _catalogCountLabel = rootVisualElement.Q<Label>("catalog-count");
            _packageListView = rootVisualElement.Q<ListView>("package-list");
            _catalogLoadingState = rootVisualElement.Q<VisualElement>("catalog-loading-state");
            _catalogEmptyState = rootVisualElement.Q<VisualElement>("catalog-empty-state");
            _catalogEmptyTitleLabel = rootVisualElement.Q<Label>("catalog-empty-title");
            _catalogEmptyBodyLabel = rootVisualElement.Q<Label>("catalog-empty-body");
            _detailsRoot = rootVisualElement.Q<VisualElement>("details-root");
            _emptyState = rootVisualElement.Q<VisualElement>("empty-state");
            _headerCard = rootVisualElement.Q<VisualElement>("header-card");
            _headerTitleLabel = rootVisualElement.Q<Label>("header-title");
            _headerPackageIdLabel = rootVisualElement.Q<Label>("header-package-id");
            _headerStatusChip = rootVisualElement.Q<Label>("header-status-chip");
            _showInExplorerButton = rootVisualElement.Q<Button>("show-in-explorer-button");
            _authorValueLabel = rootVisualElement.Q<Label>("author-value");
            _currentVersionValueLabel = rootVisualElement.Q<Label>("current-version-value");
            _latestVersionValueLabel = rootVisualElement.Q<Label>("latest-version-value");
            _descriptionValueLabel = rootVisualElement.Q<Label>("description-value");
            _operationBanner = rootVisualElement.Q<HelpBox>("operation-banner");
            _primaryActionCard = rootVisualElement.Q<VisualElement>("primary-action-card");
            _primaryActionMessage = rootVisualElement.Q<HelpBox>("primary-action-message");
            _primaryActionSummaryLabel = rootVisualElement.Q<Label>("primary-action-summary");
            _primaryActionButton = rootVisualElement.Q<Button>("primary-action-button");
            _versionsSection = rootVisualElement.Q<VisualElement>("versions-section");
            _versionsList = rootVisualElement.Q<ScrollView>("versions-list");
            _dependenciesSection = rootVisualElement.Q<VisualElement>("dependencies-section");
            _dependenciesList = rootVisualElement.Q<ScrollView>("dependencies-list");

            if (_refreshButton != null)
            {
                _refreshButton.clicked += RefreshPackageList;
            }

            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt =>
                {
                    ApplyFilter(evt.newValue);
                });
            }

            ConfigurePackageList();

            if (_showInExplorerButton != null)
            {
                _showInExplorerButton.clicked += () =>
                {
                    if (_selectedPackage != null &&
                        _selectedPackage.IsInstalled &&
                        !string.IsNullOrEmpty(_selectedPackage.InstalledPackage.resolvedPath))
                    {
                        EditorUtility.RevealInFinder(_selectedPackage.InstalledPackage.resolvedPath);
                    }
                };
            }

            if (_primaryActionButton != null)
            {
                _primaryActionButton.clicked += OnPrimaryActionClicked;
            }
        }

        private void ConfigurePackageList()
        {
            if (_packageListView == null) return;

            _packageListView.itemsSource = _filteredPackages;
            _packageListView.selectionType = SelectionType.Single;
            _packageListView.fixedItemHeight = 58;
            _packageListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _packageListView.makeItem = MakePackageListItem;
            _packageListView.bindItem = BindPackageListItem;
            _packageListView.selectionChanged += OnPackageSelectionChanged;
        }

        private VisualElement MakePackageListItem()
        {
            var root = new VisualElement();
            root.AddToClassList("package-list__item");

            var textColumn = new VisualElement();
            textColumn.AddToClassList("package-list__text");

            var title = new Label();
            title.AddToClassList("package-list__title");

            var packageId = new Label();
            packageId.AddToClassList("package-list__id");

            textColumn.Add(title);
            textColumn.Add(packageId);

            var statusChip = new Label();
            statusChip.AddToClassList("status-chip");
            statusChip.AddToClassList("package-list__status");

            root.Add(textColumn);
            root.Add(statusChip);

            root.userData = new PackageListItemElements
            {
                Root = root,
                Title = title,
                PackageId = packageId,
                StatusChip = statusChip
            };

            return root;
        }

        private void BindPackageListItem(VisualElement element, int index)
        {
            if (!(element.userData is PackageListItemElements row) || index < 0 || index >= _filteredPackages.Count) return;

            PackageCatalogEntry package = _filteredPackages[index];
            row.Title.text = package.EffectiveDisplayName;
            row.PackageId.text = package.Name;

            bool hasUpdate = package.HasUpdate(IsNewer);
            row.StatusChip.text = hasUpdate ? "Update" : package.IsInstalled ? "Installed" : "Not installed";

            SetStatusChipVariant(row.StatusChip, package.IsInstalled, hasUpdate);
        }

        private void OnPackageSelectionChanged(IEnumerable<object> selectedItems)
        {
            _selectedPackage = selectedItems.FirstOrDefault() as PackageCatalogEntry;
            RefreshViewState();
        }

        private void ApplyFilter(string searchText)
        {
            _filteredPackages.Clear();

            IEnumerable<PackageCatalogEntry> query = _packages;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(package =>
                    package.EffectiveDisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    package.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _filteredPackages.AddRange(query);
            RefreshListView();
            RestoreSelectionAfterRefresh();
            RefreshViewState();
        }

        private void RefreshPackageList()
        {
            EditorApplication.update -= ProgressListRequest;
            _listRequest = Client.List(true);
            EditorApplication.update += ProgressListRequest;

            _isCatalogLoading = true;
            SetRefreshEnabled(false);
            RefreshCatalogPaneState();
        }

        private void ProgressListRequest()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;

            if (_listRequest.Status == StatusCode.Success)
            {
                BuildCatalog(_listRequest.Result);
                ApplyFilter(_searchField?.value);
                ResolvePendingOperation();
            }
            else
            {
                Debug.LogError($"Failed to list packages: {_listRequest.Error.message}");
                SetOperationMessage($"Failed to list packages: {_listRequest.Error.message}", MessageType.Error);
            }

            _isCatalogLoading = false;
            EditorApplication.update -= ProgressListRequest;
            SetRefreshEnabled(true);
            RefreshViewState();
        }

        private void BuildCatalog(PackageCollection installedPackages)
        {
            string previousSelectionName = _selectedPackage?.Name;
            var installedByName = installedPackages
                .Where(package => package.name.StartsWith("com.ryx", StringComparison.Ordinal))
                .ToDictionary(package => package.name, package => package);

            var catalogByName = new Dictionary<string, PackageCatalogEntry>(StringComparer.Ordinal);

            foreach (var remoteTag in GitUtils.GetRemoteTags())
            {
                if (!TryParsePackageTag(remoteTag.Name, out var packageName, out var version)) continue;

                if (!catalogByName.TryGetValue(packageName, out var entry))
                {
                    entry = new PackageCatalogEntry
                    {
                        Name = packageName
                    };
                    catalogByName.Add(packageName, entry);
                }

                entry.AvailableVersions.Add(new PackageVersionInfo
                {
                    Version = version,
                    GitRef = remoteTag.GitRef
                });
            }

            foreach (var entry in catalogByName.Values)
            {
                entry.AvailableVersions = entry.AvailableVersions
                    .OrderByDescending(version => TryParseVersion(version.Version))
                    .ToList();

                HydrateRemoteMetadata(entry);
            }

            foreach (var installedPackage in installedByName.Values)
            {
                if (!catalogByName.TryGetValue(installedPackage.name, out var entry))
                {
                    entry = new PackageCatalogEntry
                    {
                        Name = installedPackage.name
                    };
                    catalogByName.Add(installedPackage.name, entry);
                }

                entry.InstalledPackage = installedPackage;

                if (string.IsNullOrEmpty(entry.DisplayName))
                {
                    entry.DisplayName = installedPackage.displayName;
                }

                if (string.IsNullOrEmpty(entry.Description))
                {
                    entry.Description = installedPackage.description;
                }

                if (string.IsNullOrEmpty(entry.AuthorName) && installedPackage.author != null)
                {
                    entry.AuthorName = installedPackage.author.name;
                }
            }

            _packages.Clear();
            _packages.AddRange(catalogByName.Values
                .OrderBy(package => package.EffectiveDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase));

            _selectedPackage = _packages.FirstOrDefault(package => package.Name == previousSelectionName) ?? _packages.FirstOrDefault();
        }

        private void HydrateRemoteMetadata(PackageCatalogEntry entry)
        {
            if (!entry.HasRemoteVersions) return;

            string manifestJson = GitUtils.GetRemotePackageManifest(entry.Name, entry.AvailableVersions[0].GitRef);
            if (string.IsNullOrEmpty(manifestJson)) return;

            PackageManifestData manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<PackageManifestData>(manifestJson);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[Ryx] Failed to parse remote manifest for {entry.Name}: {exception.Message}");
            }

            if (manifest == null) return;

            entry.Name = string.IsNullOrEmpty(manifest.name) ? entry.Name : manifest.name;
            entry.DisplayName = manifest.displayName;
            entry.Description = manifest.description;
            entry.AuthorName = manifest.author != null ? manifest.author.name : null;
        }

        private void RefreshViewState()
        {
            if (_catalogCountLabel != null)
            {
                _catalogCountLabel.text = $"{_filteredPackages.Count} packages";
            }

            RefreshCatalogPaneState();

            if (_detailsRoot == null || _emptyState == null)
            {
                return;
            }

            if (_operationBanner != null)
            {
                bool hasOperationMessage = !string.IsNullOrEmpty(_operationMessage);
                _operationBanner.text = hasOperationMessage ? _operationMessage : string.Empty;
                _operationBanner.messageType = ToHelpBoxMessageType(_operationMessageType);
                SetDisplay(_operationBanner, hasOperationMessage);
            }

            bool hasSelection = _selectedPackage != null && _filteredPackages.Contains(_selectedPackage);
            SetDisplay(_detailsRoot, hasSelection);
            SetDisplay(_emptyState, !hasSelection);

            if (!hasSelection)
            {
                if (_emptyState != null)
                {
                    var title = _emptyState.Q<Label>("empty-state-title");
                    var body = _emptyState.Q<Label>("empty-state-body");
                    if (title != null) title.text = _packages.Count == 0 ? "No packages found" : "Select a package";
                    if (body != null) body.text = _packages.Count == 0
                        ? "The remote catalog is empty or could not be loaded."
                        : "Choose a package from the catalog to inspect versions, status, and actions.";
                }

                return;
            }

            BindDetails(_selectedPackage);
        }

        private void RefreshCatalogPaneState()
        {
            if (_packageListView == null) return;

            bool hasItems = _filteredPackages.Count > 0;
            bool hasLoadError = !_isCatalogLoading && _operationMessageType == MessageType.Error && _packages.Count == 0;

            SetDisplay(_packageListView, !_isCatalogLoading && hasItems);
            SetDisplay(_catalogLoadingState, _isCatalogLoading);
            SetDisplay(_catalogEmptyState, !_isCatalogLoading && !hasItems);

            if (_catalogEmptyTitleLabel == null || _catalogEmptyBodyLabel == null || _isCatalogLoading) return;

            if (hasLoadError)
            {
                _catalogEmptyTitleLabel.text = "Could not load package catalog";
                _catalogEmptyBodyLabel.text = "The remote catalog request failed. Try Refresh again after checking connectivity and repository access.";
                return;
            }

            if (_packages.Count == 0)
            {
                _catalogEmptyTitleLabel.text = "No packages found";
                _catalogEmptyBodyLabel.text = "The remote catalog returned no Ryx packages.";
                return;
            }

            _catalogEmptyTitleLabel.text = "No matching packages";
            _catalogEmptyBodyLabel.text = "Adjust the search query or refresh the catalog to see available packages.";
        }

        private void BindDetails(PackageCatalogEntry package)
        {
            _headerTitleLabel.text = package.EffectiveDisplayName;
            _headerPackageIdLabel.text = package.Name;
            _headerStatusChip.text = package.HasUpdate(IsNewer) ? "Update available" : package.IsInstalled ? "Installed" : "Not installed";
            SetStatusChipVariant(_headerStatusChip, package.IsInstalled, package.HasUpdate(IsNewer));

            _authorValueLabel.text = package.EffectiveAuthorName;
            _currentVersionValueLabel.text = package.IsInstalled ? package.InstalledVersion : "Not installed";
            _latestVersionValueLabel.text = string.IsNullOrEmpty(package.LatestVersion) ? "Unavailable" : package.LatestVersion;
            _descriptionValueLabel.text = package.EffectiveDescription;

            SetDisplay(_showInExplorerButton, package.IsInstalled && !string.IsNullOrEmpty(package.InstalledPackage.resolvedPath));

            BindPrimaryAction(package);
            BindVersions(package);
            BindDependencies(package);
        }

        private void BindPrimaryAction(PackageCatalogEntry package)
        {
            _primaryActionButton.SetEnabled(!_isApplyingVersion);

            ClearBannerClasses(_primaryActionCard);

            if (!package.HasRemoteVersions)
            {
                _primaryActionSummaryLabel.text = "No installable remote versions were found for this package.";
                _primaryActionMessage.text = "The package is visible in the catalog, but there is no valid tagged version available for installation.";
                _primaryActionMessage.messageType = HelpBoxMessageType.Warning;
                _primaryActionButton.text = "No Action Available";
                _primaryActionButton.SetEnabled(false);
                _primaryActionCard.AddToClassList(BannerWarningClass);
                return;
            }

            if (package.HasUpdate(IsNewer))
            {
                _primaryActionSummaryLabel.text = $"A newer version is available: {package.LatestVersion}.";
                _primaryActionMessage.text = "The installed version is behind the latest tagged release.";
                _primaryActionMessage.messageType = HelpBoxMessageType.Info;
                _primaryActionButton.text = $"Update to {package.LatestVersion}";
                _primaryActionButton.SetEnabled(!_isApplyingVersion);
                _primaryActionCard.AddToClassList(BannerInfoClass);
                return;
            }

            if (!package.IsInstalled)
            {
                _primaryActionSummaryLabel.text = $"This package is not installed. Latest version: {package.LatestVersion}.";
                _primaryActionMessage.text = "Install the latest tagged release directly from the remote catalog.";
                _primaryActionMessage.messageType = HelpBoxMessageType.Info;
                _primaryActionButton.text = $"Install {package.LatestVersion}";
                _primaryActionButton.SetEnabled(!_isApplyingVersion);
                _primaryActionCard.AddToClassList(BannerInfoClass);
                return;
            }

            _primaryActionSummaryLabel.text = $"This package is installed and up to date at {package.InstalledVersion}.";
            _primaryActionMessage.text = "You can still switch to a specific tagged version from the versions list below.";
            _primaryActionMessage.messageType = HelpBoxMessageType.None;
            _primaryActionButton.text = "Installed";
            _primaryActionButton.SetEnabled(false);
        }

        private void BindVersions(PackageCatalogEntry package)
        {
            _versionsList.Clear();
            SetDisplay(_versionsSection, true);

            if (!package.HasRemoteVersions)
            {
                var label = new Label("No tagged versions available.");
                label.AddToClassList("details-list__empty");
                _versionsList.Add(label);
                return;
            }

            foreach (var version in package.AvailableVersions)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("details-list__row");

                var versionLabel = new Label(version.Version);
                versionLabel.AddToClassList("details-list__row-label");

                var actionButton = new Button();
                actionButton.AddToClassList("details-list__row-button");

                if (package.IsInstalled && version.Version == package.InstalledVersion)
                {
                    actionButton.text = "Installed";
                    actionButton.SetEnabled(false);
                }
                else
                {
                    actionButton.text = package.IsInstalled ? "Switch to" : "Install";
                    actionButton.SetEnabled(!_isApplyingVersion);

                    string targetVersion = version.Version;
                    actionButton.clicked += () => ApplyVersion(package, targetVersion);
                }

                row.Add(versionLabel);
                row.Add(actionButton);
                row.userData = new VersionRowElements
                {
                    VersionLabel = versionLabel,
                    ActionButton = actionButton
                };

                _versionsList.Add(row);
            }
        }

        private void BindDependencies(PackageCatalogEntry package)
        {
            _dependenciesList.Clear();

            bool showDependencies = package.IsInstalled &&
                package.InstalledPackage.dependencies != null &&
                package.InstalledPackage.dependencies.Length > 0;

            SetDisplay(_dependenciesSection, showDependencies);
            if (!showDependencies) return;

            foreach (var dependency in package.InstalledPackage.dependencies)
            {
                var row = new VisualElement();
                row.AddToClassList("details-list__row");

                var dependencyName = new Label(dependency.name);
                dependencyName.AddToClassList("details-list__row-label");

                var dependencyVersion = new Label(dependency.version);
                dependencyVersion.AddToClassList("details-list__row-meta");

                row.Add(dependencyName);
                row.Add(dependencyVersion);
                _dependenciesList.Add(row);
            }
        }

        private void OnPrimaryActionClicked()
        {
            if (_selectedPackage == null || _isApplyingVersion) return;

            if (_selectedPackage.HasUpdate(IsNewer))
            {
                ApplyVersion(_selectedPackage, _selectedPackage.LatestVersion);
                return;
            }

            if (!_selectedPackage.IsInstalled && _selectedPackage.HasRemoteVersions)
            {
                ApplyVersion(_selectedPackage, _selectedPackage.LatestVersion);
            }
        }

        private bool TryParsePackageTag(string tagName, out string packageName, out string version)
        {
            packageName = null;
            version = null;

            if (string.IsNullOrEmpty(tagName) || !tagName.StartsWith("com.ryx.", StringComparison.Ordinal)) return false;

            int separatorIndex = tagName.LastIndexOf('/');
            if (separatorIndex <= 0 || separatorIndex >= tagName.Length - 1) return false;

            packageName = tagName.Substring(0, separatorIndex);
            version = tagName.Substring(separatorIndex + 1);
            return IsValidTagVersion(version);
        }

        private Version TryParseVersion(string value)
        {
            if (string.IsNullOrEmpty(value)) return new Version(0, 0, 0);

            string cleanVersion = value.Split('-')[0];
            if (Version.TryParse(cleanVersion, out var result)) return result;
            return new Version(0, 0, 0);
        }

        private bool IsValidTagVersion(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            string cleanVersion = value.Split('-')[0];
            return Version.TryParse(cleanVersion, out _);
        }

        private bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current)) return false;
            return TryParseVersion(latest) > TryParseVersion(current);
        }

        private void ApplyVersion(PackageCatalogEntry package, string version)
        {
            if (_isApplyingVersion || (_addRequest != null && !_addRequest.IsCompleted)) return;

            string packageUrl = $"{GitUtils.RepositoryUrl}?path=/Packages/{package.Name}#{package.Name}/{version}";
            SessionState.SetString(PendingPackageNameKey, package.Name);
            SessionState.SetString(PendingPackageVersionKey, version);

            _isApplyingVersion = true;
            _pendingPackageUrl = packageUrl;
            SetOperationMessage(
                package.IsInstalled
                    ? $"Applying {package.EffectiveDisplayName} {version}..."
                    : $"Installing {package.EffectiveDisplayName} {version}...",
                MessageType.Info);

            try
            {
                _addRequest = Client.Add(_pendingPackageUrl);
                _pendingPackageUrl = null;
            }
            catch (Exception exception)
            {
                _pendingPackageUrl = null;
                Debug.LogException(exception);
                FailPendingOperation(exception.Message);
                RefreshViewState();
                return;
            }

            EditorApplication.update -= ProgressAddRequest;
            EditorApplication.update += ProgressAddRequest;
            RefreshViewState();
        }

        private void ProgressAddRequest()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[Ryx] Installed {_addRequest.Result.name} {_addRequest.Result.version}");
                CompletePendingOperation(_addRequest.Result.name, _addRequest.Result.version);
                RefreshPackageList();
            }
            else
            {
                string error = _addRequest.Error.message;
                Debug.LogError($"[Ryx] Failed to update package: {error}");
                FailPendingOperation(error);
                RefreshViewState();
            }

            EditorApplication.update -= ProgressAddRequest;
        }

        private void RestorePendingOperation()
        {
            string packageName = SessionState.GetString(PendingPackageNameKey, "");
            string version = SessionState.GetString(PendingPackageVersionKey, "");
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version)) return;

            _isApplyingVersion = true;
            SetOperationMessage($"Finishing installation of {packageName} {version}...", MessageType.Info);
        }

        private void ResolvePendingOperation()
        {
            if (_addRequest != null) return;

            string packageName = SessionState.GetString(PendingPackageNameKey, "");
            string version = SessionState.GetString(PendingPackageVersionKey, "");
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version)) return;

            var package = _packages.FirstOrDefault(entry => entry.Name == packageName);
            if (package != null && package.IsInstalled && package.InstalledVersion == version)
            {
                CompletePendingOperation(package.EffectiveDisplayName, version);
                return;
            }

            FailPendingOperation($"Installed version of {packageName} is not {version}. Check the Unity Console for Package Manager errors.");
        }

        private void CompletePendingOperation(string packageName, string version)
        {
            ClearPendingOperation();
            SetOperationMessage($"Installed {packageName} {version}.", MessageType.Info);
            RefreshViewState();
        }

        private void FailPendingOperation(string error)
        {
            ClearPendingOperation();
            SetOperationMessage($"Package installation failed: {error}", MessageType.Error);
            RefreshViewState();
        }

        private void ClearPendingOperation()
        {
            SessionState.EraseString(PendingPackageNameKey);
            SessionState.EraseString(PendingPackageVersionKey);
            _isApplyingVersion = false;
        }

        private void SetOperationMessage(string message, MessageType messageType)
        {
            _operationMessage = message;
            _operationMessageType = messageType;
        }

        private void SetRefreshEnabled(bool enabled)
        {
            if (_refreshButton != null)
            {
                _refreshButton.SetEnabled(enabled && !_isApplyingVersion);
            }
        }

        private void RefreshListView()
        {
            if (_packageListView == null) return;

            _packageListView.itemsSource = _filteredPackages;
            _packageListView.Rebuild();
        }

        private void RestoreSelectionAfterRefresh()
        {
            if (_packageListView == null)
            {
                RefreshViewState();
                return;
            }

            if (_selectedPackage == null)
            {
                if (_filteredPackages.Count > 0)
                {
                    _selectedPackage = _filteredPackages[0];
                }
                else
                {
                    _packageListView.ClearSelection();
                    return;
                }
            }

            int selectedIndex = _filteredPackages.FindIndex(package => package.Name == _selectedPackage.Name);
            if (selectedIndex < 0)
            {
                _selectedPackage = _filteredPackages.FirstOrDefault();
                selectedIndex = _selectedPackage == null ? -1 : 0;
            }

            if (selectedIndex >= 0)
            {
                _packageListView.SetSelection(selectedIndex);
                _packageListView.ScrollToItem(selectedIndex);
            }
            else
            {
                _packageListView.ClearSelection();
            }
        }

        private void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetStatusChipVariant(Label chip, bool isInstalled, bool hasUpdate)
        {
            if (chip == null) return;

            chip.EnableInClassList(StatusInstalledClass, isInstalled && !hasUpdate);
            chip.EnableInClassList(StatusNotInstalledClass, !isInstalled);
            chip.EnableInClassList(StatusUpdateClass, hasUpdate);
        }

        private void ClearBannerClasses(VisualElement element)
        {
            if (element == null) return;

            element.RemoveFromClassList(BannerInfoClass);
            element.RemoveFromClassList(BannerWarningClass);
            element.RemoveFromClassList(BannerErrorClass);
        }

        private HelpBoxMessageType ToHelpBoxMessageType(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Info:
                    return HelpBoxMessageType.Info;
                case MessageType.Warning:
                    return HelpBoxMessageType.Warning;
                case MessageType.Error:
                    return HelpBoxMessageType.Error;
                default:
                    return HelpBoxMessageType.None;
            }
        }
    }
}
