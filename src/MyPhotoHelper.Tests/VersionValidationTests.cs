using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace MyPhotoHelper.Tests
{
    [TestClass]
    public class VersionValidationTests
    {
        [TestMethod]
        public async Task ProductVersion_ShouldMatchAutoUpdaterVersion()
        {
            // Arrange - Get the main MyPhotoHelper assembly
            var mainAssemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyPhotoHelper.dll");
            Assert.IsTrue(File.Exists(mainAssemblyPath), $"MyPhotoHelper.dll should exist at: {mainAssemblyPath}");
            
            var assembly = Assembly.LoadFrom(mainAssemblyPath);
            Assert.IsNotNull(assembly, "Could not load MyPhotoHelper assembly");

            // Get ProductVersion from the main assembly
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var productVersion = fileVersionInfo.ProductVersion;
            
            Assert.IsNotNull(productVersion, "ProductVersion should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(productVersion), "ProductVersion should not be empty");

            // Clean the product version (remove any Git hash suffix)
            var cleanProductVersion = productVersion.Contains('+') 
                ? productVersion.Split('+')[0] 
                : productVersion;

            // Get AutoUpdater version from update.xml
            var updateXmlPath = Path.Combine(GetRepositoryRoot(), "update.xml");
            Assert.IsTrue(File.Exists(updateXmlPath), $"update.xml file should exist at: {updateXmlPath}");

            var updateXmlContent = await File.ReadAllTextAsync(updateXmlPath);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(updateXmlContent);

            var versionNode = xmlDoc.SelectSingleNode("//version");
            Assert.IsNotNull(versionNode, "Version node should exist in update.xml");

            var autoUpdaterVersion = versionNode.InnerText.Trim();
            Assert.IsFalse(string.IsNullOrWhiteSpace(autoUpdaterVersion), "AutoUpdater version should not be empty");

            // Convert versions to comparable format (remove trailing .0 if present)
            var normalizedProductVersion = NormalizeVersion(cleanProductVersion);
            var normalizedAutoUpdaterVersion = NormalizeVersion(autoUpdaterVersion);

            // Assert
            Assert.AreEqual(normalizedAutoUpdaterVersion, normalizedProductVersion, 
                $"ProductVersion ('{cleanProductVersion}') should match AutoUpdater version ('{autoUpdaterVersion}'). " +
                $"This indicates a version synchronization issue between the assembly and update.xml. " +
                $"Please update both files to have matching versions before release.");
        }

        [TestMethod]
        public void ProductVersion_ShouldNotContainGitHash()
        {
            // Arrange - Get the main MyPhotoHelper assembly
            var mainAssemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyPhotoHelper.dll");
            Assert.IsTrue(File.Exists(mainAssemblyPath), $"MyPhotoHelper.dll should exist at: {mainAssemblyPath}");
            
            var assembly = Assembly.LoadFrom(mainAssemblyPath);
            Assert.IsNotNull(assembly, "Could not load MyPhotoHelper assembly");

            // Get ProductVersion from the main assembly
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var productVersion = fileVersionInfo.ProductVersion;

            // Assert
            Assert.IsNotNull(productVersion, "ProductVersion should not be null");
            Assert.IsFalse(productVersion.Contains('+'), 
                $"ProductVersion ('{productVersion}') should not contain Git hash information. " +
                $"This indicates that IncludeSourceRevisionInInformationalVersion is not properly disabled. " +
                $"Users will see a confusing version number in the About dialog.");
        }

        [TestMethod]
        public void ProductVersion_ShouldFollowSemanticVersioning()
        {
            // Arrange - Get the main MyPhotoHelper assembly
            var mainAssemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyPhotoHelper.dll");
            Assert.IsTrue(File.Exists(mainAssemblyPath), $"MyPhotoHelper.dll should exist at: {mainAssemblyPath}");
            
            var assembly = Assembly.LoadFrom(mainAssemblyPath);
            Assert.IsNotNull(assembly, "Could not load MyPhotoHelper assembly");

            // Get ProductVersion from the main assembly
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var productVersion = fileVersionInfo.ProductVersion;
            
            Assert.IsNotNull(productVersion, "ProductVersion should not be null");

            // Clean the product version (remove any Git hash suffix)
            var cleanProductVersion = productVersion.Contains('+') 
                ? productVersion.Split('+')[0] 
                : productVersion;

            // Assert - should be in format X.Y.Z or X.Y.Z.W
            Assert.IsTrue(System.Version.TryParse(cleanProductVersion, out var version), 
                $"ProductVersion ('{cleanProductVersion}') should be a valid version number (e.g., 1.2.3 or 1.2.3.0)");

            Assert.IsNotNull(version, "Version should not be null after parsing");
            Assert.IsTrue(version.Major >= 1, "Major version should be at least 1");
            Assert.IsTrue(version.Minor >= 0, "Minor version should be non-negative");
            Assert.IsTrue(version.Build >= 0, "Build version should be non-negative");
        }

        private string GetRepositoryRoot()
        {
            // Start from the test assembly location and walk up to find the repository root
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            Assert.IsNotNull(assemblyDir, "Assembly directory should not be null");
            
            var currentDir = new DirectoryInfo(assemblyDir);
            
            while (currentDir != null)
            {
                // Look for update.xml file (indicating repository root)
                if (File.Exists(Path.Combine(currentDir.FullName, "update.xml")))
                {
                    return currentDir.FullName;
                }
                
                // Look for .git directory (alternative indicator)
                if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
                {
                    return currentDir.FullName;
                }
                
                currentDir = currentDir.Parent;
            }
            
            throw new DirectoryNotFoundException("Could not find repository root. Expected to find update.xml or .git directory.");
        }

        private string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return version;

            // Parse as Version to normalize format
            if (System.Version.TryParse(version, out var parsedVersion))
            {
                // Return as X.Y.Z format (removing the build number if it's 0)
                if (parsedVersion.Build == 0 && parsedVersion.Revision == -1)
                {
                    return $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}";
                }
                else if (parsedVersion.Revision == -1)
                {
                    return $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}";
                }
                else
                {
                    return $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}.{parsedVersion.Revision}";
                }
            }

            return version;
        }
    }
}