using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.ProjectModel;
using NuGet.Protocol.Plugins;

namespace DependenSee
{
    /// <summary>
    /// Discovery service using project.assets.json file as its data source
    /// </summary>
    public class ProjectAssetsDiscoveryService
    {
        private const string PROJECT_ASSETS_FILE_NAME = "project.assets.json";
        private bool _recurse;

        private HashSet<string> _packageExcludes = new HashSet<string>();
        private HashSet<string> _packageIncludes = new HashSet<string>();

        private bool _hasPackageExcludes = false;
        private bool _hasPackageIncludes = false;

        private HashSet<string> _projectExcludes = new HashSet<string>();
        private HashSet<string> _projectIncludes = new HashSet<string>();

        private bool _hasProjectExcludes = false;
        private bool _hasProjectIncludes = false;

        /// <summary>
        /// Root directory for the project
        /// </summary>
        public string RootDirectory { get; set; }

        /// <summary>
        /// Flag indicating whether to graph inbound references rather than outbound (the default)
        /// </summary>
        public bool DiscoverInboundReferences { get; set; }

        /// <summary>
        /// Comma-separated list of package prefixes or complete names to exclude from discovery. All not in 
        /// the list will be included.
        /// </summary>
        public string ExcludePackageNamespaces 
        { 
            get
            {
                return string.Join(',', _packageExcludes.ToList());
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _hasPackageExcludes = true;
                    var tokens = value.Split(',');
                    for (int i=0; i < tokens.Length; ++i)
                    {
                        _packageExcludes.Add(tokens[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Comma-separated list of package prefixes or complete names to include in discovery. All not in 
        /// the list will be excluded.
        /// </summary>
        public string IncludePackageNamespaces 
        { 
            get
            {
                return string.Join(',', _packageIncludes.ToList());
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _hasPackageIncludes = true;
                    var tokens = value.Split(',');
                    for (int i=0; i < tokens.Length; ++i)
                    {
                        _packageIncludes.Add(tokens[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Comma-separated list of project prefixes or complete names to exclude from discovery. All not in 
        /// the list will be included.
        /// </summary>
        public string ExcludeProjectNamespaces 
        { 
            get
            {
                return string.Join(',', _projectExcludes.ToList());
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _hasProjectExcludes = true;
                    var tokens = value.Split(',');
                    for (int i=0; i < tokens.Length; ++i)
                    {
                        _projectExcludes.Add(tokens[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Comma-separated list of project prefixes or complete names to include in discovery. All not in 
        /// the list will be excluded.
        /// </summary>
        public string IncludeProjectNamespaces 
        { 
            get
            {
                return string.Join(',', _projectIncludes.ToList());
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _hasProjectIncludes = true;
                    var tokens = value.Split(',');
                    for (int i=0; i < tokens.Length; ++i)
                    {
                        _projectIncludes.Add(tokens[i].Trim());
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance using the specified parameters
        /// </summary>
        /// <param name="rootDirectory"></param>
        /// <param name="assetsDirectory"></param>
        public ProjectAssetsDiscoveryService(string rootDirectory, bool recurse = true)
        {
            RootDirectory = rootDirectory;
            _recurse = recurse;   
        }

        /// <summary>
        /// Discovers projects and package references at the root and below if recursion is enabled. 
        /// </summary>
        /// <returns><see cref="DiscoveryResult"/> representing the results of the discovery operation.</returns>
        public DiscoveryResult Discover()
        {
            var references = new List<Reference>();
            var packages = new List<Package>();
            var projects = new List<Project>();

            if (DiscoverInboundReferences)
            {
                DiscoverInboundPackageRefs(RootDirectory, ref projects, ref packages, ref references);
                return new DiscoveryResult()
                {
                    References = references,
                    Packages = packages,
                    Projects = projects
                };
            }
            else
            {
                DiscoverOutboundPackageRefs(RootDirectory, ref projects, ref packages, ref references);
                return new DiscoveryResult()
                {
                    References = references,
                    Packages = packages,
                    Projects = projects
                };
            }
        }

        /// <summary>
        /// Discovers projects and outbound package references at the root and below if recursion is enabled
        /// </summary>
        /// <returns><see cref="DiscoveryResult"/> representing the results of the discovery operation.</returns>
        private void DiscoverOutboundPackageRefs(
            string rootDirectory, 
            ref List<Project> projects, 
            ref List<Package> packages, 
            ref List<Reference> references,
            bool inbound = false)
        {
            // process this directory
            if (Directory.GetFiles(rootDirectory, "*.csproj").Length > 0 || Directory.GetFiles(rootDirectory, "*.vbproj").Length > 0)
            {
                // directory contains a project file
                var assetsDirectory = Path.Combine(rootDirectory, "obj");
                var assetsFile = Path.Combine(assetsDirectory, PROJECT_ASSETS_FILE_NAME);

                if (File.Exists(assetsFile))
                {
                    var assetsObj = LockFileUtilities.GetLockFile(assetsFile, null);
                    bool addProject = IncludeProjectByFilter(assetsObj.PackageSpec.Name);

                    if (addProject)
                    {
                        var project = new Project()
                        {
                            Name = assetsObj.PackageSpec.Name,
                            Id = $"{assetsObj.PackageSpec.Name} {assetsObj.PackageSpec.Version}"
                        };

                        projects.AddIfNotExists(project);

                        foreach (var library in assetsObj.Libraries.Where(l => l.Type == "package"))
                        {
                            if(IncludePackageByFilter(library.Name))
                            {
                                var package = new Package()
                                {
                                    Name = $"{library.Name} {library.Version}",
                                    Id = $"{library.Name} {library.Version}"
                                };
                                packages.AddIfNotExists(package);

                                references.AddIfNotExists(new Reference()
                                {
                                    From = project.Id,
                                    To = package.Id
                                });
                            }
                        }
                    }
                }
            }
            
            if (_recurse)
            {
                // process child directories
                foreach (var subDir in Directory.GetDirectories(rootDirectory))
                {
                    DiscoverOutboundPackageRefs(subDir, ref projects, ref packages, ref references);
                }
            }
        }
        /// <summary>
        /// Discovers projects and inbound package references at the root and below if recursion is enabled
        /// </summary>
        /// <returns><see cref="DiscoveryResult"/> representing the results of the discovery operation.</returns>
        private void DiscoverInboundPackageRefs(
            string rootDirectory, 
            ref List<Project> projects, 
            ref List<Package> packages, 
            ref List<Reference> references,
            bool inbound = false)
        {
            // process this directory
            if (Directory.GetFiles(rootDirectory, "*.csproj").Length > 0 || Directory.GetFiles(rootDirectory, "*.vbproj").Length > 0)
            {
                // directory contains a project file
                var assetsDirectory = Path.Combine(rootDirectory, "obj");
                var assetsFile = Path.Combine(assetsDirectory, PROJECT_ASSETS_FILE_NAME);

                if (File.Exists(assetsFile))
                {
                    var assetsObj = LockFileUtilities.GetLockFile(assetsFile, null);
                    if(IncludeProjectByFilter(assetsObj.PackageSpec.Name))
                    {
                        var project = new Project()
                        {
                            Name = assetsObj.PackageSpec.Name,
                            Id = $"{assetsObj.PackageSpec.Name} {assetsObj.PackageSpec.Version}"
                        };
                        projects.AddIfNotExists(project);

                        foreach (var library in assetsObj.Libraries)
                        {
                            if(IncludePackageByFilter(library.Name))
                            {
                                var package = new Package()
                                {
                                    Name = $"{library.Name} {library.Version}",
                                    Id = $"{library.Name} (package) {library.Version}"
                                };
                                packages.AddIfNotExists(package);

                                references.AddIfNotExists(new Reference()
                                {
                                    From = package.Id,
                                    To = project.Id
                                });
                            }
                        }
                    }
                }    
            }
            if (_recurse)
            {
                // process child directories
                foreach (var subDir in Directory.GetDirectories(rootDirectory))
                {
                    DiscoverInboundPackageRefs(subDir, ref projects, ref packages, ref references);
                }
            }
        }

        /// <summary>
        /// Determines if a package should be included by filter specified in argv.
        /// </summary>
        /// <remarks>Matches via String.StartsWith() so supports namespace prefixes. No support for wildcards of RegEx matching</remarks>
        /// <param name="packageName">Name of the package</param>
        /// <returns>True if the package should be included, else false.</returns>
        private bool IncludePackageByFilter(string packageName)
        {
            bool addPackage = true;
            if (_hasPackageIncludes)
            {
                addPackage = false;
                foreach (var include in _packageIncludes)
                {
                    if (packageName.StartsWith(include))
                    {
                        addPackage = true;
                        break;
                    }
                }
                foreach (var exclude in _packageExcludes)
                {
                    if (packageName.StartsWith(exclude))
                    {
                        addPackage = false;
                        break;
                    }
                }
            }
            return addPackage;
        }

        /// <summary>
        /// Determines if a project should be included by filter specified in argv.
        /// </summary>
        /// <remarks>Matches via String.StartsWith() so supports namespace prefixes. No support for wildcards of RegEx matching</remarks>
        /// <param name="projectName">Name of the project</param>
        /// <returns>True if the project should be included, else false.</returns>
         private bool IncludeProjectByFilter(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                return false;
            }

            bool addProject = true;
            if (_hasProjectIncludes)
            {
                addProject = false;
                foreach (var include in _projectIncludes)
                {
                    if (projectName.StartsWith(include))
                    {
                        addProject = true;
                        break;
                    }
                }
                foreach (var exclude in _projectExcludes)
                {
                    if (projectName.StartsWith(exclude))
                    {
                        addProject = false;
                        break;
                    }
                }
            }
            return addProject;
        }
    }
}