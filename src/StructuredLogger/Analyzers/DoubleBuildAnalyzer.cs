using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class DoubleBuildAnalyzer
    {
        private Dictionary<string, List<Project>> projectMap = new();

        public void AddProject(Project project)
        {
            if (project.EvaluationId < 0)
            {
                return;
            }

            string outputFile = GetOutputFile(project);

            if (!string.IsNullOrEmpty(outputFile))
            {
                if (projectMap.TryGetValue(outputFile, out List<Project> existingProject))
                {
                    existingProject.Add(project);
                }
                else
                {
                    projectMap[outputFile] = new List<Project>() { project };
                }
            }
        }

        public void AppendDoubleBuildFolder(Build build)
        {
            Folder doubleWrites = null;
            Folder diagFolder = null;
            foreach (var kvp in projectMap)
            {
                if (kvp.Value.Count > 1)
                {
                    var outputFile = kvp.Key;
                    List<Project> results = kvp.Value;

                    doubleWrites = doubleWrites ?? build.GetOrCreateNodeWithName<Folder>("DoubleBuild");
                    var item = new Item { Text = outputFile };
                    doubleWrites.AddChild(item);

                    foreach (var project in results)
                    {
                        // var projectText = new Item { Text = $"{project.Name} + {project.EvaluationId}" };
                        item.AddChild(project);
                    }
                }
                /*
                else
                {
                    var outputFile = kvp.Key;
                    diagFolder = diagFolder ?? build.GetOrCreateNodeWithName<Folder>("DB Diag");
                    var item = new Item { Text = outputFile };
                    diagFolder.AddChild(item);
                }
                */
            }
        }

        private string GetOutputFile(Project project)
        {
            // Graph Build puts "Forces unique project identity in the MSBuild engine" property
            var global = project.FindFirstChild<Folder>(t => t.Name == "Properties");
            if (global != null)
            {
                global = global.FindFirstChild<Folder>(f => f.Name == "Global");
                if (global != null)
                {
                    var globalProp = global.FindFirstChild<Property>(p => p.Value == "Forces unique project identity in the MSBuild engine");
                    if (globalProp != null)
                    {
                        return string.Empty;
                    }
                }
            }
            
            Target target;
            if (project.Name.EndsWith(".csproj") || project.Name.EndsWith(".vbproj"))
            {
                target = project.FindFirstChild<Target>(t => t.Name == "CoreCompile");
                if (target == null)
                {
                    return string.Empty;
                }

                target = project.FindFirstChild<Target>(t => t.Name == "GetTargetPathWithTargetPlatformMoniker");
                if (target == null)
                {
                    return string.Empty;
                }

                var addItem = target.FindFirstChild<AddItem>(ad => ad.Name == "TargetPathWithTargetPlatformMoniker");
                if (addItem == null)
                {
                    return string.Empty;
                }

                return (addItem.FirstChild as Item).Text;
            }
            else if (project.Name.EndsWith(".vcxproj") || project.Name.EndsWith(".nativeproj"))
            {
                target = project.FindFirstChild<Target>(t => t.Name == "Lib");
                if (target == null)
                {
                    target = project.FindFirstChild<Target>(t => t.Name == "Link");
                }

                if (target == null)
                {
                    return string.Empty;
                }

                var task = target.FindChild<Task>(task => task.Name == "Message");
                if (task != null)
                {
                    string message = (task.Children.First() as Message).Text;
                    int index = message.IndexOf("-> ");
                    if (index != -1)
                    {
                        return message.Substring(index + 3);
                    }
                }
            }

            return string.Empty;
        }
    }
}
