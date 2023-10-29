using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Converters;
using Frosty.Core.Interfaces;
using Frosty.Core.Misc;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FrostyNM
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Iterates over every element in a <see cref="List{CString}"/> and adds a string conversion of such an element to a returned list.
        /// </summary>
        /// <param name="cStringList">The <see cref="CString"/> list to be used.</param>
        /// <returns>A list of strings consisting of <see cref="CString"/> string conversions.</returns>
        public static List<string> NormalizeCStringList(this List<CString> cStringList)
        {
            // Create a list of strings for storing converted strings from the provided cstring list
            List<string> stringList = new List<string>();

            // Begin an iteration over the collection of cstrings
            for (int i = 0; i < cStringList.Count; i++)
            {
                // Convert the iterator's current selection to a string and add it to the list of converted strings
                stringList.Add(cStringList[i].ToString());
            }

            // Return the converted list
            return stringList;
        }

        /// <summary>
        /// Enumerates through the asset entries of a type provided via an existing <see cref="AssetEntry"/>.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="AssetEntry"/> to enumerate through.</typeparam>
        /// <param name="sourceEntry">The source asset entry to be used.</param>
        /// <param name="modifiedOnly">A bool determining whether or not modified assets should only be enumerated for any applicable enumeration method.</param>
        /// <param name="customAssetBranch">The type to be used in custom asset enumeration.</param>
        /// <returns>A list of the enumerated assets.</returns>
        public static List<T> EnumerateByEntryType<T>(this AssetManager assetManager, T sourceEntry, bool modifiedOnly = false, string customAssetBranch = "legacy")
        {
            // Create a list of elements with the provided type
            List<T> enumeratedEntries = new List<T>();

            // Begin a switch statement over the source entry's type name
            switch (sourceEntry.GetType().Name)
            {
                case "AssetEntry":
                    // Add the results of an enumeration over custom assets to the list of enumerated entries
                    enumeratedEntries.AddRange((IEnumerable<T>)assetManager.EnumerateCustomAssets(customAssetBranch));

                    // Break this case
                    break;

                case "ChunkAssetEntry":
                    // Add the results of an enumeration over chunks to the list of enumerated entries
                    enumeratedEntries.AddRange((IEnumerable<T>)assetManager.EnumerateChunks(modifiedOnly));

                    // Break this case
                    break;

                case "EbxAssetEntry":
                    // Append the elements of an enumeration over the available asset entries to the associated output list
                    enumeratedEntries.AddRange((IEnumerable<T>)assetManager.EnumerateEbx("", modifiedOnly));

                    // Break this case
                    break;

                case "ResAssetEntry":
                    // Add the res enumeration results to the associated output list
                    enumeratedEntries.AddRange((IEnumerable<T>)assetManager.EnumerateRes(0, modifiedOnly));

                    // Break this case
                    break;
            }

            // Return the list of enumerated entries
            return enumeratedEntries;
        }

        /// <summary>
        /// Exports an asset's dependencies.
        /// </summary>
        /// <param name="ebx">The <see cref="EbxAsset"/> to be used.</param>
        /// <param name="outputPath">The output path to export the assets to.</param>
        /// <param name="doRecursively">Determines whether or not recursive export should be used.</param>
        /// <param name="createDirectories">Determines whether or not assets will be exported to a single directory or numerous subdirectories.</param>
        /// <param name="exportDefinition">The export <see cref="AssetDefinition"/> to be used.</param>
        /// <param name="extension">The file extension to be used for determining the export type.</param>
        public static void ExportAssetDependencies(this AssetManager assetManager, EbxAsset ebx, string outputPath, bool createDirectories = false, bool doRecursively = false, AssetDefinition exportDefinition = null, string extension = null)
        {
            // Ported from old dnSpy Frosty edits, altered to accommodate for 1.0.6.10 needs, changes, and the new extension type
            // The method has also been commented for easier understanding and long-term documentation

            // Check if there's dependencies to export
            if (ebx.Dependencies.Count() != 0)
            {
                // Begin an iteration over each dependency GUID
                foreach (Guid dependencyGuidEntry in ebx.Dependencies)
                {
                    // Create an asset entry for storing the associated asset entry of the dependency
                    EbxAssetEntry dependencyAssetEntry = assetManager.GetEbxEntry(dependencyGuidEntry);

                    // Create an ebx asset for storing the entry's associated asset
                    EbxAsset dependencyEbx = assetManager.GetEbx(dependencyAssetEntry);

                    // Create a stream for storing the dependency's ebx stream
                    Stream dependencyEbxStream = assetManager.GetEbxStream(dependencyAssetEntry);

                    // Create a string for storing the export directory path; these are always appended with _Dependencies
                    string dependencyOutputPath = createDirectories ? Path.Combine(new string[]
                    {
                        outputPath,
                        string.Concat(new string[]
                        {
                            App.AssetManager.GetEbxEntry(ebx.FileGuid).Filename,
                            "_Dependencies"
                        })
                    }) : outputPath;

                    // Create the export directory
                    Directory.CreateDirectory(dependencyOutputPath);

                    // Create a bool for storing the AssetDefinition export result
                    bool exportSuccessful = false;

                    // Check if there's a valid list of export types and if there's an assigned filter
                    if (exportDefinition != null && extension != null)
                    {
                        // Attempt to export the asset and assign to the exportSuccessful bool
                        exportSuccessful = exportDefinition.BetterExport(dependencyAssetEntry, dependencyOutputPath, extension);
                    }

                    // Check if the external export process was successful
                    if (!exportSuccessful)
                    {
                        // Create a NativeWriter for writing the output EBX
                        using (NativeWriter nativeWriter = new NativeWriter(new FileStream(string.Concat(new string[]
                        {
                            Path.Combine(new string[]
                            {
                                dependencyOutputPath,
                                dependencyAssetEntry.Filename
                            }),
                            ".bin"
                        }), FileMode.Create, FileAccess.Write)))
                        {
                            // Write the asset stream to the FileStream
                            nativeWriter.Write(NativeReader.ReadInStream(dependencyEbxStream));
                        }
                    }

                    // Check if recursive export should be used
                    if (doRecursively)
                    {
                        // Recursively export any available dependencies of the current dependency
                        assetManager.ExportAssetDependencies(dependencyEbx, dependencyOutputPath, createDirectories, doRecursively, exportDefinition, extension);
                    }
                }
            }
        }

        /// <summary>
        /// Registers key bindings to a <see cref="CommandBindingCollection"/> via a dictionary of <see cref="KeyGesture"/>/<see cref="ExecutedRoutedEventHandler"/> pairings.
        /// </summary>
        /// <param name="gestureHandlerPairings">The dictionary of key/handler pairings to be used.</param>
        public static void RegisterKeyBindings(this CommandBindingCollection commandBindingCollection, Dictionary<KeyGesture, ExecutedRoutedEventHandler> gestureHandlerPairings)
        {
            // Create a RoutedCommand for storing the created command instances of the iteration
            RoutedCommand currentCommand;

            // Begin an iteration over the provided dictionary
            for (int i = 0; i < gestureHandlerPairings.Count; i++)
            {
                // Assign to the iteration's current RoutedCommand
                currentCommand = new RoutedCommand();

                // Register the iteration's selected key to the new command
                currentCommand.InputGestures.Add(gestureHandlerPairings.Keys.ElementAt(i));

                // Register a CommandBinding with the use of the newly-created RoutedCommand
                commandBindingCollection.Add(new CommandBinding(currentCommand, gestureHandlerPairings.Values.ElementAt(i)));
            }
        }

        /// <summary>
        /// Gets the value of a <see cref="PointerRef"/>.
        /// </summary>
        /// <param name="pointerRef">The <see cref="PointerRef"/> to be used.</param>
        /// <returns>The resolved value.</returns>
        public static object Resolve(this PointerRef pointerRef)
        {
            // Create a variable for storing the returned pointerref value
            object pointerRefValue = null;

            // Check if it's external
            if (pointerRef.Type == PointerRefType.External)
            {
                // Get the pointerref's external ebximportreference
                EbxImportReference importReference = pointerRef.External;

                // Get the associated asset and asset entry
                EbxAssetEntry importEntry = App.AssetManager.GetEbxEntry(importReference.FileGuid);
                EbxAsset importAsset = App.AssetManager.GetEbx(importEntry);

                // Set the pointerref's value to its import reference's referenced object
                pointerRefValue = importAsset.GetObject(importReference.ClassGuid);
            }
            else if (pointerRef.Type == PointerRefType.Internal)
            {
                // Set the pointerref's value to its internal value
                pointerRefValue = pointerRef.Internal;
            }

            // If it isn't either of these, it is a null pointerref, so nothing has to be set since the pointerref's value is defaulted to null
            // Return the value
            return pointerRefValue;
        }

        /// <summary>
        /// Initiates a search on an object via a provided list of properties and search operations, returning its findings once the operation is complete.
        /// </summary>
        /// <param name="refObject">The object to be used in the search.</param>
        /// <param name="targetProperties">The properties to search through until the last array element is reached.</param>
        /// <param name="returnNullIfNoResults">A bool determining whether or not null should be returned if no results have been found.</param>
        /// <param name="shouldResolveRefs">A bool determining whether or not <see cref="PointerRef"/> instances should be resolved. This may not apply to all scenarios, as resolving a <see cref="PointerRef"/> will be necessary at times.</param>
        /// <returns>The value of the last property in the <paramref name="targetProperties"/> array. If one of the properties is a <see cref="IEnumerable"/>, a list of the results will be returned.</returns>
        public static object Search(this object refObject, List<string> targetProperties, bool returnNullIfNoResults = false, bool shouldResolveRefs = true)
        {
            // Ported from 1.0.5.10, now with alterations to improve efficiency and to adapt to changes

            // Create an object for storing the search result
            object searchResult = null;

            // Check the provided parameters list either for being null or having no elements (which can be the case for the target properties list)
            if (refObject != null && targetProperties != null && targetProperties.Count != 0)
            {
                // Store the object's type
                Type objectType = refObject.GetType();

                // Store the current target property's name
                string currentPropertyName = targetProperties[0];

                // Store a variation of the current property's name that's been split by ":", which would indicate there's a search operation if there's elements
                string[] splittedPropertyName = currentPropertyName.Split(':');

                // Create a string for storing a potential search operation
                string searchOperation = "";

                // Check if there's more than one element in the results
                if (splittedPropertyName.Length > 1)
                {
                    // Store the search operation as a lowercase variant
                    searchOperation = splittedPropertyName[0].ToLower();

                    // Begin a switch statement on the potential search operation
                    switch (searchOperation)
                    {
                        // Check if the search operation is an includes operation
                        case "includes":
                            // Remove the search operation from the property name
                            currentPropertyName = splittedPropertyName[splittedPropertyName.Length - 1];

                            // Do nothing, as the remaining steps of the operation will now be passed after the currentProperty != null check
                            break;

                        // Check if the search operation is a type check with subclasses operation
                        case "type_subclasses":
                            // Store a splitted variation of the splitted property name's second element to gain a potential array of types
                            string[] types = splittedPropertyName[splittedPropertyName.Length - 1].Split(',');

                            // Create a bool that indicates the success of this operation
                            bool isSuccess = false;

                            // Begin an iteration over the types
                            for (int i = 0; i < types.Length; i++)
                            {
                                // Check if the input object's type is a subclass of the selected type or the same type
                                if (TypeLibrary.IsSubClassOf(refObject, types[i]))
                                {
                                    // Set the is success boolean to true
                                    isSuccess = true;

                                    // Break the iteration if it is
                                    break;
                                }
                            }

                            // Return a result based on the success of the iteration
                            return isSuccess ? refObject : null;
                    }
                }

                // Create a property info variable for storing the secondary iteration's current property
                PropertyInfo currentProperty = objectType.GetProperty(currentPropertyName);

                // Create an object for storing the current property's value
                object propertyValue;

                // Remove the first element from the properties list to allow for it to be passed to recursive operations if one should be removed
                targetProperties.RemoveAt(0);

                // Check if the current property's info isn't null
                if (currentProperty != null)
                {
                    // Store the value of the current property
                    propertyValue = currentProperty.GetValue(refObject);

                    // Check if the type of search method is an includes operation
                    if (searchOperation == "includes")
                    {
                        // The property has already had a null check, so the operation is already known to be successful

                        // Set the search result to a recursive search on the input object
                        searchResult = refObject.Search(targetProperties);

                        // Return the result of a recursive operation on the input object
                        return searchResult;
                    }

                    // Check if the value is of a list
                    if (propertyValue is IList)
                    {
                        // Create a list for storing the potential of multiple results
                        List<object> enumerationResults = new List<object>();
                        // Cast the property's value to an IEnumerable
                        IEnumerable propertyEnumerable = (IEnumerable)propertyValue;
                        // Get the enumerator
                        IEnumerator propertyEnumerator = propertyEnumerable.GetEnumerator();
                        // Create an object for storing the enumerator's current selection
                        object enumeratorSelection;
                        // Create a list for storing a copy of the target properties
                        List<string> subTargetProperties;

                        // Create an object for storing the sub-recursive search results
                        object secondarySearchResult;
                        // Get a bool representing whether or not pointerrefs should be resolved by checking if the amount of properties is greater than or equal to 2
                        bool resolveRefs = targetProperties.Count >= 2;

                        // Begin the enumeration
                        while (propertyEnumerator.MoveNext())
                        {
                            // Set the enumerator's current selection
                            enumeratorSelection = propertyEnumerator.Current;
                            // Set the sub target properties to the target properties
                            subTargetProperties = new List<string>(targetProperties);

                            // Check if the selection is a pointerref and if a property will be searched for in the resolved object
                            if (enumeratorSelection is PointerRef && targetProperties.Count >= 1)
                            {
                                // Set the selection to the resolved value
                                enumeratorSelection = ((PointerRef)enumeratorSelection).Resolve();
                            }

                            // Get the enumerator's current selection, perform a recursive search over it, and set the secondary search results to its results
                            secondarySearchResult = enumeratorSelection.Search(subTargetProperties, true, resolveRefs);

                            // Check if this search result isn't null
                            if (secondarySearchResult != null)
                            {
                                // Add it to the enumeration results
                                enumerationResults.Add(secondarySearchResult);
                            }
                        }

                        // Clear the target properties
                        targetProperties.Clear();

                        // Set the search result to the enumeration results
                        searchResult = enumerationResults;
                    }
                    else
                    {
                        // Check if the property's value is a pointerref
                        if (propertyValue is PointerRef && shouldResolveRefs)
                        {
                            // Set the search result to the result of a recursive operation on the resolved value
                            searchResult = ((PointerRef)propertyValue).Resolve().Search(targetProperties);
                        }
                        else
                        {
                            // Pass the property's value and the property list of this operation to a secondary operation and set the result
                            searchResult = propertyValue.Search(targetProperties);
                        }
                    }
                }
                else
                {
                    // Set the search result to the input object if the property was null
                    searchResult = returnNullIfNoResults ? null : refObject;
                }
            }
            else
            {
                // Set the search result to the input object if anything was null or an empty list
                searchResult = returnNullIfNoResults ? null : refObject;
            }

            // Return the search result of this operation
            return searchResult;
        }

        // Methods listed below were not originally in ClassExtensions; they were added to assist with the methods listed above and below

        /// <summary>
        /// Improves upon <see cref="AssetDefinition.Export(EbxAssetEntry, string, string)"/> to automatically handle an export path.
        /// </summary>
        /// <param name="entry">The asset entry to be exported.</param>
        /// <param name="path">The export path to be used.</param>
        /// <param name="filterType">The export type to be used.</param>
        /// <returns>A bool indicating the success of this operation.</returns>
        public static bool BetterExport(this AssetDefinition assetDefinition, EbxAssetEntry entry, string path, string filterType)
        {
            // Ported from 1.0.6.10, written into an extension since the original method cannot be modified

            // Updated method to automatically handle a path if it's detected to be a directory
            // This shouldn't break plugins due to how it's written

            // Check if a directory exists from the path
            if (Directory.Exists(path))
            {
                // Assign to the path with a formatted variation
                path = string.Format("{0}.{1}", Path.Combine(new string[]
                {
                    path,
                    entry.Filename
                }), filterType);
            }

            // Pass this method's parameters to the real export method
            return assetDefinition.Export(entry, path, filterType);
        }

        /// <summary>
        /// Retrieves the value of a field within a class regardless of its access modifiers.
        /// </summary>
        /// <param name="targetField">The field name to be used.</param>
        public static object GetFieldValue(this object refObject, string targetField)
        {
            // Create a type for storing the current type selection
            Type currentType = refObject.GetType();

            // Create a FieldInfo instance for storing the retrieved instance
            FieldInfo objectField = null;

            // Begin a loop based on the condition of the object's current type not being null and its field being null
            while (currentType != null && objectField == null)
            {
                // Assign to the object field with the current FieldInfo retrieval attempt
                objectField = currentType.GetField(targetField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                // Assign to the object type with the next base type
                currentType = currentType.BaseType;
            }

            // Return a result based on the object field being null
            return objectField != null ? objectField.GetValue(refObject) : null;
        }

        // Methods listed below were also not originally in ClassExtensions; they were moved to this class for cleanliness within the plugin's internals

        /// <summary>
        /// Adds a NameValue pair to this <see cref="CustomComboData{T, U}"/>.
        /// </summary>
        /// <param name="name">The name to be added.</param>
        /// <param name="value">The optional value to be added. If null, the name will simply be used as the value if both <see cref="List{T}"/> instances use the same element type.</param>
        public static void AddNameValuePair<T, U>(this CustomComboData<T, U> customComboData, U name, T value = default)
        {
            // Add the provided name to the collection of names
            customComboData.Names.Add(name);

            // Add the provided value if it's assigned; otherwise, use the name
            customComboData.Values.Add(value != null && !value.Equals(default) ? value : (T)Convert.ChangeType(name, typeof(T)));
        }

        /// <summary>
        /// Opens a variety of assets from a provided list of asset entries.
        /// </summary>
        /// <param name="entries">The collection of asset entries to be used.</param>
        /// <param name="createDefaultEditors">A bool determining whether or not the base asset editor should be used by all newly-created tabs.</param>
        /// <param name="passthroughTask">An optional <see cref="FrostyTaskWindow"/> passthrough parameter for current asset status alterations.</param>
        public static void OpenAssets(this IEditorWindow editorWindow, List<AssetEntry> entries, bool createDefaultEditors, FrostyTaskWindow passthroughTask = null)
        {
            // Adjusted method to accommodate for 1.0.6.0's limitations

            // Create an asset entry for storing the iteration's current selection
            AssetEntry currentEntry;

            // Begin an iteration over the provided collection of asset entries
            for (int i = 0; i < entries.Count; i++)
            {
                // Assign to the current entry
                currentEntry = entries[i];

                // Check if the current entry isn't null
                if (currentEntry != null)
                {
                    // Open the asset using the new method overload
                    editorWindow.OpenAsset(currentEntry, createDefaultEditors);

                    // Check if the passthrough task isn't null
                    if (passthroughTask != null)
                    {
                        // Update the task if it is
                        passthroughTask.Update(currentEntry.Filename, (i + 1) / entries.Count * 100);
                    }
                }
            }
        }

        /// <summary>
        /// Closes all <see cref="FrostyTabItem"/> instances that are found to be using an asset from a provided list of asset entries.
        /// </summary>
        /// <param name="sourceEntries">The list of <see cref="AssetEntry"/> instances to be used.</param>
        /// <returns>An int representing the total quantity of tabs closed.</returns>
        public static int RemoveTabs(this IEditorWindow editorWindow, List<AssetEntry> sourceEntries, bool onlyModified = false)
        {
            // Adjusted this method to also accommodate for 1.0.6.0's requirements
            // Create a dynamic variable for dynamic evaluation of the editor window
            dynamic realEditorWindow = editorWindow;

            // Create an int for storing the amount of closed tabs
            int closedTabs = 0;

            // Create a FrostyTabControl for storing the retrieved tab control
            FrostyTabControl mainTabControl = (FrostyTabControl)editorWindow.GetFieldValue("tabControl");

            // Create a FrostyTabItem for storing the iteration's current selection
            FrostyTabItem currentSelection;

            // Begin an iteration over the list of asset entries
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                // Begin an iteration over the list of opened tabs
                for (int j = 0; j < mainTabControl.Items.Count; j++)
                {
                    // Assign to the current selection
                    currentSelection = (FrostyTabItem)mainTabControl.Items[j];

                    // Check if the selected asset entry matches the tab's opened asset
                    if (sourceEntries[i].Name == currentSelection.TabId && (!onlyModified || sourceEntries[i].IsModified))
                    {
                        // Remove the tab
                        realEditorWindow.RemoveTab(currentSelection);

                        // Increment the amount of removed FrostyTabItems
                        closedTabs++;

                        // Break this iteration
                        break;
                    }
                }
            }

            // Return the amount of tabs closed
            return closedTabs;
        }
    }
}
