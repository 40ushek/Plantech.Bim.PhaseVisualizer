#region Assembly Tekla.Structures.Model, Version=2021.0.0.0, Culture=neutral, PublicKeyToken=2f04dbe497b71114
// location unknown
// Decompiled with ICSharpCode.Decompiler 8.2.0.7535
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Tekla.Structures.Filtering;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Internal;
using Tekla.Structures.ModelInternal;
using Tekla.Structures.Solid;

namespace Tekla.Structures.Model.Operations;

//
// Summary:
//     The Operation class implements Tekla Structures level operations.
[Serializable]
[ClassInterface(ClassInterfaceType.AutoDual)]
[Guid("E3973C22-AA6F-42AB-ABCB-25C3469FC388")]
public static class Operation
{
    //
    // Summary:
    //     The object manipulation types.
    private enum ObjectManipulationTypeEnum
    {
        DOT_MOVE_OBJECT_WITH_VECTOR,
        DOT_MOVE_OBJECT_WITH_COORDINATESYSTEM,
        DOT_COPY_OBJECT_WITH_VECTOR,
        DOT_COPY_OBJECT_WITH_COORDINATESYSTEM,
        DOT_SPLIT_OBJECT,
        DOT_COMBINE_PARTS,
        DOT_COMBINE_REBARS,
        DOT_UNGROUP_REBARS,
        DOT_GROUP_REBARS
    }

    //
    // Summary:
    //     The MIS export types.
    public enum MISExportTypeEnum
    {
        //
        // Summary:
        //     The DSTV type.
        DSTV,
        //
        // Summary:
        //     The KISS type.
        KISS,
        //
        // Summary:
        //     The EJE type.
        EJE,
        //
        // Summary:
        //     The EPC type.
        EPC,
        //
        // Summary:
        //     The STEEL2000 type.
        STEEL2000
    }

    //
    // Summary:
    //     Specifies what Tekla.Structures.Model.Operations.Operation.ShowOnlySelected(Tekla.Structures.Model.Operations.Operation.UnselectedModeEnum)
    //     should do to unselected parts.
    public enum UnselectedModeEnum
    {
        //
        // Summary:
        //     Completely hide.
        Hidden,
        //
        // Summary:
        //     Make almost transparent.
        Transparent,
        //
        // Summary:
        //     Show as sticks.
        AsSticks
    }

    //
    // Summary:
    //     The result type of the shape metadata operations. If you alter this, check if
    //     you need to change ShapeMetadataResult_e on the Tekla Structures core side
    public enum ShapeMetadataResult
    {
        //
        // Summary:
        //     Operation failed.
        NoResult,
        //
        // Summary:
        //     Operation succeeded.
        OK,
        //
        // Summary:
        //     At least one identical pre-existing key was found in the shape when trying to
        //     insert key and value
        DuplicateKeyExist,
        //
        // Summary:
        //     No matching shape found for the GUID in the catalog
        NoMatchingShape,
        //
        // Summary:
        //     No matching key found for the GUID in the shape
        NoMatchingKey
    }

    //
    // Summary:
    //     The ProgressBar class implements progress bar with cancel button.
    public class ProgressBar
    {
        internal enum OperationEnum
        {
            DISPLAY = 1,
            CLOSE,
            SET_PROGRESS,
            QUERY_CANCELED
        }

        private bool progressBarDisplayed;

        //
        // Summary:
        //     Display progress bar dialog with cancel button. Display will fail if progress
        //     bar is already displayed.
        //
        // Parameters:
        //   SleepTime:
        //     Time (ms) to wait until bar is displayed.
        //
        //   Title:
        //     Title of the dialog.
        //
        //   Message:
        //     Message to be displayed on the dialog above progress bar.
        //
        //   CancelButtonLabel:
        //     Label of cancel button.
        //
        //   ProgressLabel:
        //     Initial progress label (updated with SetProgress). If empty of null no bar exists.
        //
        //
        // Returns:
        //     True if bar was displayed successfully (meaning bar must be closed later).
        public bool Display(int SleepTime, string Title, string Message, string CancelButtonLabel, string ProgressLabel)
        {
            if (!progressBarDisplayed)
            {
                dotProgressBar_t pProgressBar = default(dotProgressBar_t);
                pProgressBar.Operation = 1;
                pProgressBar.SleepTime = SleepTime;
                pProgressBar.aTitle = Title;
                pProgressBar.aMessage = Message;
                pProgressBar.aCancelButtonLabel = CancelButtonLabel;
                pProgressBar.aProgressLabel = ProgressLabel;
                progressBarDisplayed = ((DelegateProxy.Delegate.ExportProgressBarOperation(ref pProgressBar) != 0) ? true : false);
            }

            return progressBarDisplayed;
        }

        //
        // Summary:
        //     Close progress bar. Can be called even if Display was not successful.
        public void Close()
        {
            if (progressBarDisplayed)
            {
                dotProgressBar_t pProgressBar = default(dotProgressBar_t);
                pProgressBar.Operation = 2;
                DelegateProxy.Delegate.ExportProgressBarOperation(ref pProgressBar);
            }
        }

        //
        // Summary:
        //     Update status information on the progress bar.
        //
        // Parameters:
        //   ProgressLabel:
        //     Bar label text.
        //
        //   Progress:
        //     Progess, number between 0..100
        public void SetProgress(string ProgressLabel, int Progress)
        {
            if (progressBarDisplayed)
            {
                dotProgressBar_t pProgressBar = default(dotProgressBar_t);
                pProgressBar.Operation = 3;
                pProgressBar.aProgressLabel = ProgressLabel;
                pProgressBar.Progress = Progress;
                DelegateProxy.Delegate.ExportProgressBarOperation(ref pProgressBar);
            }
        }

        //
        // Summary:
        //     Check if cancel has been pressed.
        //
        // Returns:
        //     True if cancel has been pressed.
        public bool Canceled()
        {
            if (!progressBarDisplayed)
            {
                return false;
            }

            dotProgressBar_t pProgressBar = default(dotProgressBar_t);
            pProgressBar.Operation = 4;
            DelegateProxy.Delegate.ExportProgressBarOperation(ref pProgressBar);
            if (pProgressBar.Canceled != 0)
            {
                return true;
            }

            return false;
        }
    }

    internal const int ENUM_DEFAULT_SIZE = 100;

    //
    // Summary:
    //     Inits the filtering cache
    internal static void InitFilterCache()
    {
        DelegateProxy.Delegate.ExportInitFilterCache();
    }

    //
    // Summary:
    //     Clears the filtering cache
    internal static void ClearFilterCache()
    {
        DelegateProxy.Delegate.ExportClearFilterCache();
    }

    //
    // Summary:
    //     Checks whether the numbering is up-to-date for an assembly, part, rebar, surface
    //     treatment, pour object or break.
    //
    // Parameters:
    //   InputModelObject:
    //     The model object to check. The object must be an assembly, a part, a rebar or
    //     an inherited object.
    //
    // Returns:
    //     True if the numbering information is up-to-date.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown if the InputModelObject is not of a correct type or if it is not valid.
    public static bool IsNumberingUpToDate(ModelObject InputModelObject)
    {
        if (InputModelObject == null)
        {
            throw new ArgumentNullException("ModelObject");
        }

        if (!InputModelObject.Identifier.IsValid())
        {
            throw new ArgumentException("Identifier not valid");
        }

        bool flag = InputModelObject.GetType() == typeof(Part) || InputModelObject.GetType().IsSubclassOf(typeof(Part)) || InputModelObject.GetType().IsSubclassOf(typeof(Reinforcement));
        if (!flag)
        {
            ModelObject.ModelObjectEnum modelObjectEnum = TypeMapper.MapTypesToIntList(new Type[1] { InputModelObject.GetType() })[0];
            if (modelObjectEnum != ModelObject.ModelObjectEnum.SURFACE_TREATMENT && modelObjectEnum != ModelObject.ModelObjectEnum.ASSEMBLY && (uint)(modelObjectEnum - 47) > 1u)
            {
                throw new ArgumentException("Unsupported type for numbering check: " + modelObjectEnum);
            }

            flag = true;
        }

        if (flag)
        {
            dotNumberingQuery_t dotNumberingQuery_t = default(dotNumberingQuery_t);
            dotNumberingQuery_t.QueryMode = NumberingQueryModeEnum.SINGLE_ID;
            dotNumberingQuery_t.Id = InputModelObject.Identifier.ID;
            dotNumberingQuery_t pNumberingQuery = dotNumberingQuery_t;
            if (DelegateProxy.Delegate.ExportGetNumberingUpToDate(ref pNumberingQuery) > 0)
            {
                return true;
            }
        }

        return false;
    }

    //
    // Summary:
    //     Checks whether the numbering is up-to-date for every assembly, part and rebar
    //     on the model. Using this method is much faster than checking each object individually.
    //
    //
    // Returns:
    //     True if the numbering information is up-to-date.
    public static bool IsNumberingUpToDateAll()
    {
        dotNumberingQuery_t dotNumberingQuery_t = default(dotNumberingQuery_t);
        dotNumberingQuery_t.QueryMode = NumberingQueryModeEnum.ALL_PARTS_REBARS_ASSEMBLIES;
        dotNumberingQuery_t.Id = 0;
        dotNumberingQuery_t pNumberingQuery = dotNumberingQuery_t;
        return DelegateProxy.Delegate.ExportGetNumberingUpToDate(ref pNumberingQuery) > 0;
    }

    //
    // Summary:
    //     Gets similar objects based on numbering of given object.
    //
    // Parameters:
    //   ObjectToCompare:
    //     The object for comparison.
    //
    // Returns:
    //     List of similar objects.
    //
    // Remarks:
    //     This method works currently only with parts and assemblies.
    public static List<ModelObject> GetSimilarNumberedObjects(ModelObject ObjectToCompare)
    {
        List<ModelObject> list = new List<ModelObject>();
        if (ObjectToCompare == null)
        {
            throw new ArgumentNullException();
        }

        if (ObjectToCompare is Part || ObjectToCompare is Assembly)
        {
            if (ObjectToCompare.Identifier.IsValid())
            {
                dotIdentifier_t pIdentifier = default(dotIdentifier_t);
                pIdentifier.ToStruct(ObjectToCompare.Identifier);
                dotClientId_t pClientId = dotClientId_t.GetClientId();
                if (DelegateProxy.Delegate.ExportGetSimilarNumberedObjects(ref pIdentifier, ref pClientId) > 0)
                {
                    IntList intList = new IntList();
                    ListExporter.ImportIntList(intList);
                    Model model = new Model();
                    foreach (int item in intList)
                    {
                        ModelObject modelObject = model.SelectModelObject(new Identifier(item));
                        if (modelObject != null)
                        {
                            list.Add(modelObject);
                        }
                    }
                }
            }

            return list;
        }

        throw new ArgumentException("Unsupported type for numbering check: " + ObjectToCompare.GetType().ToString());
    }

    //
    // Summary:
    //     Creates a report from the selected objects using the given template name and
    //     filename.
    //
    //     If a path is not given in the filename, the file is created to the folder defined
    //     with the advanced option XS_REPORT_OUTPUT_DIRECTORY.
    //
    //     If the given folder does not exist, the report creation fails.
    //
    //     See Tekla Structures Help for more information about reports.
    //
    // Parameters:
    //   TemplateName:
    //     The name of the report template to be used in report creation. The name must
    //     contain more than three characters.
    //
    //   FileName:
    //     The name of the created report. The name must contain more than three characters.
    //
    //
    //   Title1:
    //     The first title for the created report.
    //
    //   Title2:
    //     The second title for the created report.
    //
    //   Title3:
    //     The third title for the created report.
    //
    // Returns:
    //     True if the report is created.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     Thrown when the TemplateName or FileName is null.
    //
    //   T:System.ArgumentException:
    //     Thrown when the TemplateName or FileName is too short.
    public static bool CreateReportFromSelected(string TemplateName, string FileName, string Title1, string Title2, string Title3)
    {
        return CreateReport(TemplateName, FileName, Title1, Title2, Title3, OnlySelected: true);
    }

    private static string AddDefaultExtensionAndPathForReport(string FileName)
    {
        if (!FileName.Contains("."))
        {
            FileName += ".xsr";
        }

        if (!FileName.Contains("\\"))
        {
            string Value = "";
            TeklaStructuresSettings.GetAdvancedOption("XS_REPORT_OUTPUT_DIRECTORY", ref Value);
            if (!Value.EndsWith("\\"))
            {
                Value += "\\";
            }

            FileName = Value + FileName;
        }

        return FileName;
    }

    private static bool CreateReport(string TemplateName, string FileName, string Title1, string Title2, string Title3, bool OnlySelected)
    {
        dotCreateReportFromModel_t aReport = default(dotCreateReportFromModel_t);
        bool result = true;
        if (TemplateName != null && TemplateName.Length > 3 && FileName != null && FileName.Length > 3)
        {
            FileName = AddDefaultExtensionAndPathForReport(FileName);
            aReport.aTemplateName = TemplateName.Clone() as string;
            aReport.aFileName = FileName.Clone() as string;
            aReport.aTitle1 = Title1.Clone() as string;
            aReport.aTitle2 = Title2.Clone() as string;
            aReport.aTitle3 = Title3.Clone() as string;
            aReport.OnlySelected = (OnlySelected ? 1 : 0);
            if (DelegateProxy.Delegate.ExportCreateReport(ref aReport) <= 0)
            {
                result = false;
            }
        }
        else
        {
            result = false;
            if (TemplateName == null)
            {
                throw new ArgumentNullException("TemplateName");
            }

            if (TemplateName.Length <= 3)
            {
                throw new ArgumentException("TemplateName too short.");
            }

            if (FileName == null)
            {
                throw new ArgumentNullException("FileName");
            }

            if (FileName.Length <= 3)
            {
                throw new ArgumentException("FileName too short.");
            }
        }

        return result;
    }

    //
    // Summary:
    //     Opens and displays a report with the given name.
    //
    //     If a path is not given in the filename, the file is searched from the folder
    //     defined with the advanced option XS_REPORT_OUTPUT_DIRECTORY.
    //
    //     See Tekla Structures Help for more information about reports.
    //
    // Parameters:
    //   FileName:
    //     The name of the report to display. The name must contain more than three characters.
    //
    //
    // Returns:
    //     True if the report existed.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the file specified in the FileName is not found or when the FileName
    //     is too short.
    public static bool DisplayReport(string FileName)
    {
        if (FileName != null && FileName.Length > 3)
        {
            FileName = AddDefaultExtensionAndPathForReport(FileName);
            if (DelegateProxy.Delegate.ExportDisplayReport(FileName) <= 0)
            {
                throw new ArgumentException("File not found.");
            }

            return true;
        }

        throw new ArgumentException("Invalid filename.");
    }

    //
    // Summary:
    //     Creates a report from all the objects using the given template name and filename.
    //
    //
    //     If a path is not given in the filename, the file is created to the folder defined
    //     with the advanced option XS_REPORT_OUTPUT_DIRECTORY.
    //
    //     If the given folder does not exist, the report creation fails.
    //
    //     Internally, this method is asynchronous, and because of that the output file
    //     cannot be immediately available.
    //
    //     See Tekla Structures Help for more information about reports.
    //
    // Parameters:
    //   TemplateName:
    //     The name of the report template to be used in report creation. The name must
    //     contain more than three characters.
    //
    //   FileName:
    //     The name of the created report. The name must contain more than three characters.
    //
    //
    //   Title1:
    //     The first title for the created report.
    //
    //   Title2:
    //     The second title for the created report.
    //
    //   Title3:
    //     The third title for the created report.
    //
    // Returns:
    //     True if the report is created.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     Thrown when the TemplateName or FileName is null.
    //
    //   T:System.ArgumentException:
    //     Thrown when the TemplateName or FileName is too short.
    public static bool CreateReportFromAll(string TemplateName, string FileName, string Title1, string Title2, string Title3)
    {
        return CreateReport(TemplateName, FileName, Title1, Title2, Title3, OnlySelected: false);
    }

    //
    // Summary:
    //     Creates NC files from the selected parts using the given NC template name.
    //
    //     See Tekla Structures Help for more information about NC files.
    //
    // Parameters:
    //   NCFileSettings:
    //     The name of the NC setting template to be used in creation.
    //
    //   DestinationFolder:
    //     The name of the folder where NC files are created. If defined, overrides the
    //     default folder in the setting template.
    //
    // Returns:
    //     True if the NC files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the NCFileSettings is not defined.
    //
    //   T:System.ArgumentNullException:
    //     Thrown when the NCFileSettings is null.
    public static bool CreateNCFilesFromSelected(string NCFileSettings, string DestinationFolder)
    {
        string DstvOutput = string.Empty;
        return CreateNC(NCFileSettings, DestinationFolder, OnlySelected: true, CreatePopMarks: false, "", CreateContourMarking: false, "", ref DstvOutput);
    }

    //
    // Summary:
    //     Creates NC files from the selected parts using the given NC template name.
    //
    //     See Tekla Structures Help for more information about NC files.
    //
    // Parameters:
    //   NCFileSettings:
    //     The name of the NC setting template to be used in creation.
    //
    //   DestinationFolder:
    //     The name of the folder where NC files are created. If defined, overrides the
    //     default folder in the setting template.
    //
    //   CreatePopMarks:
    //     Create pop-marks during export.
    //
    //   PopMarkSettingsFileName:
    //     The name of the pop-mark setting file to be used in creation.
    //
    //   CreateContourMarking:
    //     Create contour marking during export.
    //
    //   ContourMarkingSettingsFileName:
    //     The name of the contour marking setting file to be used in creation.
    //
    // Returns:
    //     True if the NC files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the NCFileSettings is not defined.
    //
    //   T:System.ArgumentNullException:
    //     Thrown when the NCFileSettings is null.
    public static bool CreateNCFilesFromSelected(string NCFileSettings, string DestinationFolder, bool CreatePopMarks = false, string PopMarkSettingsFileName = "", bool CreateContourMarking = false, string ContourMarkingSettingsFileName = "")
    {
        string DstvOutput = string.Empty;
        return CreateNC(NCFileSettings, DestinationFolder, OnlySelected: true, CreatePopMarks, PopMarkSettingsFileName, CreateContourMarking, ContourMarkingSettingsFileName, ref DstvOutput);
    }

    //
    // Summary:
    //     Creates NC files from all parts using the given NC template name.
    //
    //     See Tekla Structures Help for more information about NC files.
    //
    // Parameters:
    //   NCFileSettings:
    //     The name of the NC setting template to be used in creation.
    //
    //   DestinationFolder:
    //     The name of the folder where NC files are created. If defined, overrides the
    //     default folder in the setting template.
    //
    // Returns:
    //     True if the NC files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the NCFileSettings is not defined.
    //
    //   T:System.ArgumentNullException:
    //     Thrown when the NCFileSettings is null.
    public static bool CreateNCFilesFromAll(string NCFileSettings, string DestinationFolder)
    {
        string DstvOutput = string.Empty;
        return CreateNC(NCFileSettings, DestinationFolder, OnlySelected: false, CreatePopMarks: false, string.Empty, CreateContourMarking: false, string.Empty, ref DstvOutput);
    }

    //
    // Summary:
    //     Creates NC files from all parts using the given NC template name.
    //
    //     See Tekla Structures Help for more information about NC files.
    //
    // Parameters:
    //   NCFileSettings:
    //     The name of the NC setting template to be used in creation.
    //
    //   DestinationFolder:
    //     The name of the folder where NC files are created. If defined, overrides the
    //     default folder in the setting template.
    //
    //   CreatePopMarks:
    //     Create pop-marks during export.
    //
    //   PopMarkSettingsFileName:
    //     The name of the pop-mark setting file to be used in creation.
    //
    //   CreateContourMarking:
    //     Create contour marking during export.
    //
    //   ContourMarkingSettingsFileName:
    //     The name of the contour marking setting file to be used in creation.
    //
    // Returns:
    //     True if the NC files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the NCFileSettings is not defined.
    //
    //   T:System.ArgumentNullException:
    //     Thrown when the NCFileSettings is null.
    public static bool CreateNCFilesFromAll(string NCFileSettings, string DestinationFolder, bool CreatePopMarks = false, string PopMarkSettingsFileName = "", bool CreateContourMarking = false, string ContourMarkingSettingsFileName = "")
    {
        string DstvOutput = string.Empty;
        return CreateNC(NCFileSettings, DestinationFolder, OnlySelected: false, CreatePopMarks, PopMarkSettingsFileName, CreateContourMarking, ContourMarkingSettingsFileName, ref DstvOutput);
    }

    //
    // Summary:
    //     Creates NC files from the selected parts using the given NC template name.
    //
    //     See Tekla Structures Help for more information about NC files.
    //
    // Parameters:
    //   NCFileSettings:
    //     The name of the NC setting template to be used in creation.
    //
    //   DestinationFolder:
    //     The name of the folder where NC files are created. If defined, overrides the
    //     default folder in the setting template.
    //
    //   PartID:
    //     The identifier of the part.
    //
    //   DstvOutput:
    //     The DSTV output as string.
    //
    //   CreatePopMarks:
    //     Create pop-marks during export.
    //
    //   PopMarkSettingsFileName:
    //     The name of the pop-mark setting file to be used in creation.
    //
    //   CreateContourMarking:
    //     Create contour marking during export.
    //
    //   ContourMarkingSettingsFileName:
    //     The name of the contour marking setting file to be used in creation.
    //
    // Returns:
    //     True if the NC files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the NCFileSettings is not defined.
    //
    //   T:System.ArgumentNullException:
    //     Thrown when the NCFileSettings is null.
    public static bool CreateNCFilesByPartId(string NCFileSettings, string DestinationFolder, Identifier PartID, out string DstvOutput, bool CreatePopMarks = false, string PopMarkSettingsFileName = "", bool CreateContourMarking = false, string ContourMarkingSettingsFileName = "")
    {
        DstvOutput = string.Empty;
        bool result = CreateNC(NCFileSettings, DestinationFolder, OnlySelected: true, CreatePopMarks, PopMarkSettingsFileName, CreateContourMarking, ContourMarkingSettingsFileName, ref DstvOutput, PartID);
        if (string.IsNullOrEmpty(DstvOutput))
        {
            result = false;
        }

        return result;
    }

    private static bool CreateNC(string NCFileSettings, string DestinationFolder, bool OnlySelected, bool CreatePopMarks, string PopMarkFileName, bool CreateContourMarking, string ContourMarkingFileName, ref string DstvOutput, Identifier PartID = null)
    {
        dotCreateNCFromModel_t aNC = default(dotCreateNCFromModel_t);
        bool flag = true;
        if (!string.IsNullOrEmpty(NCFileSettings))
        {
            aNC.aNCFileSettingsName = NCFileSettings.Clone() as string;
            aNC.aPopMarkSettingsName = PopMarkFileName.Clone() as string;
            aNC.aContourMarkingSettingsName = ContourMarkingFileName.Clone() as string;
            aNC.aDestinationFolderName = DestinationFolder.Clone() as string;
            aNC.OnlySelected = (OnlySelected ? 1 : 0);
            aNC.CreatePopMarks = (CreatePopMarks ? 1 : 0);
            aNC.CreateContourMarking = (CreateContourMarking ? 1 : 0);
            aNC.ExportType = -1;
            if (PartID == null)
            {
                PartID = new Identifier();
            }

            aNC.PartId.ToStruct(PartID);
            flag = DelegateProxy.Delegate.ExportCreateNC(ref aNC) > 0;
            if (flag)
            {
                DstvOutput = aNC.FileOutput;
            }
        }
        else
        {
            flag = false;
            if (NCFileSettings == null)
            {
                throw new ArgumentNullException("NCFileSettings");
            }

            if (NCFileSettings.Length == 0)
            {
                throw new ArgumentException("NCFileSettings not defined.");
            }
        }

        return flag;
    }

    //
    // Summary:
    //     Creates MIS files from the selected parts using the given file name.
    //
    //     See Tekla Structures Help for more information about MIS files.
    //
    // Parameters:
    //   MISType:
    //     The type of the MIS export.
    //
    //   FileName:
    //     The name of the MIS file to be used in creation.
    //
    // Returns:
    //     True if the MIS files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     Thrown when the FileName is null.
    //
    //   T:System.ArgumentException:
    //     Thrown when the FileName is not defined.
    public static bool CreateMISFileFromSelected(MISExportTypeEnum MISType, string FileName)
    {
        return CreateMIS(MISType, FileName, OnlySelected: true);
    }

    //
    // Summary:
    //     Creates MIS files from all parts using the given file name.
    //
    //     See Tekla Structures Help for more information about MIS files.
    //
    // Parameters:
    //   MISType:
    //     The type of the MIS export.
    //
    //   FileName:
    //     The name of the MIS file to be used in creation.
    //
    // Returns:
    //     True if the MIS files are created, false if the numbering is not up-to-date or
    //     the used configuration is wrong.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     Thrown when the FileName is null.
    //
    //   T:System.ArgumentException:
    //     Thrown when the FileName is not defined.
    public static bool CreateMISFileFromAll(MISExportTypeEnum MISType, string FileName)
    {
        return CreateMIS(MISType, FileName, OnlySelected: false);
    }

    private static bool CreateMIS(MISExportTypeEnum MISType, string FileName, bool OnlySelected)
    {
        dotCreateNCFromModel_t aNC = default(dotCreateNCFromModel_t);
        bool result = true;
        if (!string.IsNullOrEmpty(FileName))
        {
            aNC.OnlySelected = (OnlySelected ? 1 : 0);
            aNC.ExportType = (int)MISType;
            aNC.aFileName = FileName;
            if (DelegateProxy.Delegate.ExportCreateNC(ref aNC) <= 1)
            {
                result = false;
            }
        }
        else
        {
            result = false;
            if (FileName == null)
            {
                throw new ArgumentNullException("FileName");
            }

            if (FileName.Length == 0)
            {
                throw new ArgumentException("FileName not defined.");
            }
        }

        return result;
    }

    //
    // Summary:
    //     Starts a macro with the given name. Throws an exception if the file is not found.
    //
    //
    //     Macros are saved as *.cs files in the folder defined with the XS_MACRO_DIRECTORY
    //     variable.
    //
    //     It is possible to run drawing macros using relative paths.
    //
    //     See Tekla Structures Help for more information about macros.
    //
    // Parameters:
    //   FileName:
    //     The name of the macro to start.
    //
    // Returns:
    //     True if the macro existed.
    public static bool RunMacro(string FileName)
    {
        return DelegateProxy.Delegate.ExportRunMacro(FileName) > 0;
    }

    //
    // Summary:
    //     Returns true if a macro is running, false otherwise.
    //
    //     Macros are saved as *.cs files in the folder defined with the XS_MACRO_DIRECTORY
    //     variable.
    //
    //     See Tekla Structures Help for more information about macros.
    //
    // Returns:
    //     True if a macro is running.
    public static bool IsMacroRunning()
    {
        bool result = false;
        if (DelegateProxy.Delegate.IsMacroRunning() > 0)
        {
            result = true;
        }

        return result;
    }

    //
    // Summary:
    //     Opens a new model to Tekla Structures ignoring auto saved information.
    //
    // Parameters:
    //   ModelFolder:
    //     The model folder to be used.
    //
    // Returns:
    //     True on success.
    [Obsolete("Use the method in ModelHandler class instead. Will be removed in near future.")]
    public static bool Open(string ModelFolder)
    {
        return Open(ModelFolder, OpenAutoSaved: false);
    }

    //
    // Summary:
    //     Opens a new model to Tekla Structures.
    //
    // Parameters:
    //   ModelFolder:
    //     The model folder to be used.
    //
    //   OpenAutoSaved:
    //     Tells whether to open auto saved information or not.
    //
    // Returns:
    //     True on success.
    [Obsolete("Use the method in ModelHandler class instead. Will be removed in near future.")]
    public static bool Open(string ModelFolder, bool OpenAutoSaved)
    {
        return new ModelHandler().Open(ModelFolder, OpenAutoSaved);
    }

    //
    // Summary:
    //     Tells whether a model has auto saved information.
    //
    // Parameters:
    //   ModelFolder:
    //     The model folder to be used.
    //
    // Returns:
    //     True if there is auto saved information.
    [Obsolete("Use the method in ModelHandler class instead. Will be removed in near future.")]
    public static bool IsModelAutoSaved(string ModelFolder)
    {
        return new ModelHandler().IsModelAutoSaved(ModelFolder);
    }

    //
    // Summary:
    //     Saves the current model as a web model.
    //
    //     You can save the model as a web model that can be viewed via the Internet using
    //     a web browser (e.g. Internet Explorer).
    //
    // Parameters:
    //   Filename:
    //     The filename to be used.
    //
    // Returns:
    //     True on success, false on failure.
    public static bool SaveAsWebModel(string Filename)
    {
        return CreateWebModel(Filename, OnlySelected: false);
    }

    //
    // Summary:
    //     Saves the selected objects as a web model.
    //
    //     You can save the selected objects as a web model that can be viewed via the Internet
    //     using a web browser (e.g. Internet Explorer).
    //
    // Parameters:
    //   Filename:
    //     The filename to be used.
    //
    // Returns:
    //     True on success, false on failure.
    public static bool SaveSelectedAsWebModel(string Filename)
    {
        return CreateWebModel(Filename, OnlySelected: true);
    }

    private static bool CreateWebModel(string Filename, bool OnlySelected)
    {
        dotSaveAsWebModel_t pSaveAsWebModel = default(dotSaveAsWebModel_t);
        bool result = false;
        pSaveAsWebModel.Filename = Filename.Clone() as string;
        pSaveAsWebModel.OnlySelected = (OnlySelected ? 1 : 0);
        if (DelegateProxy.Delegate.ExportSaveAsWebModel(ref pSaveAsWebModel) != 0)
        {
            result = pSaveAsWebModel.Result != 0;
        }

        return result;
    }

    //
    // Summary:
    //     Moves the model object using the given translation vector.
    //
    // Parameters:
    //   Object:
    //     The model object to move.
    //
    //   TranslationVector:
    //     The vector for moving the object.
    //
    // Returns:
    //     True on success, false on failure.
    //
    // Remarks:
    //     Note that the object is moved and updated in the view so ModelObject.Modify()
    //     is not needed. Call Modify() only after the object's data has been updated with
    //     the ModelObject.Select() method.
    public static bool MoveObject(ModelObject Object, Vector TranslationVector)
    {
        bool result = false;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Point1.ToStruct(TranslationVector);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 0;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            result = true;
        }

        return result;
    }

    //
    // Summary:
    //     Moves the model object between the given translation coordinate systems.
    //
    // Parameters:
    //   Object:
    //     The model object to move.
    //
    //   StartCoordinateSystem:
    //     The coordinate system to move the object from.
    //
    //   EndCoordinateSystem:
    //     The coordinate system to move the object to.
    //
    // Returns:
    //     True on success, false on failure.
    //
    // Remarks:
    //     Note that the object is moved and updated in the view so ModelObject.Modify()
    //     is not needed. Call Modify() only after the object's data has been updated with
    //     the ModelObject.Select() method.
    public static bool MoveObject(ModelObject Object, CoordinateSystem StartCoordinateSystem, CoordinateSystem EndCoordinateSystem)
    {
        bool result = false;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Point1.ToStruct(StartCoordinateSystem.Origin);
        pArgument.Point2.ToStruct(StartCoordinateSystem.Origin + StartCoordinateSystem.AxisX);
        pArgument.Point3.ToStruct(StartCoordinateSystem.Origin + StartCoordinateSystem.AxisY);
        pArgument.EndPoint1.ToStruct(EndCoordinateSystem.Origin);
        pArgument.EndPoint2.ToStruct(EndCoordinateSystem.Origin + EndCoordinateSystem.AxisX);
        pArgument.EndPoint3.ToStruct(EndCoordinateSystem.Origin + EndCoordinateSystem.AxisY);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 1;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            result = true;
        }

        return result;
    }

    //
    // Summary:
    //     Copies the model object using the given translation vector.
    //
    // Parameters:
    //   Object:
    //     The model object to copy.
    //
    //   CopyVector:
    //     The translation vector for copying.
    //
    // Returns:
    //     The copied model object on success, null on failure.
    public static ModelObject CopyObject(ModelObject Object, Vector CopyVector)
    {
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        ModelObject result = null;
        pArgument.Point1.ToStruct(CopyVector);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 2;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            Identifier identifier = new Identifier();
            pArgument.Result.FromStruct(identifier);
            result = new Model().SelectModelObject(identifier);
        }

        return result;
    }

    //
    // Summary:
    //     Copies the model object between the given translation coordinate systems.
    //
    // Parameters:
    //   Object:
    //     The model object to copy.
    //
    //   StartCoordinateSystem:
    //     The coordinate system to copy the object from.
    //
    //   EndCoordinateSystem:
    //     The coordinate system to copy the object to.
    //
    // Returns:
    //     The copied model object on success, null on failure.
    public static ModelObject CopyObject(ModelObject Object, CoordinateSystem StartCoordinateSystem, CoordinateSystem EndCoordinateSystem)
    {
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        ModelObject result = null;
        pArgument.Point1.ToStruct(StartCoordinateSystem.Origin);
        pArgument.Point2.ToStruct(StartCoordinateSystem.Origin + StartCoordinateSystem.AxisX);
        pArgument.Point3.ToStruct(StartCoordinateSystem.Origin + StartCoordinateSystem.AxisY);
        pArgument.EndPoint1.ToStruct(EndCoordinateSystem.Origin);
        pArgument.EndPoint2.ToStruct(EndCoordinateSystem.Origin + EndCoordinateSystem.AxisX);
        pArgument.EndPoint3.ToStruct(EndCoordinateSystem.Origin + EndCoordinateSystem.AxisY);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 3;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            Identifier identifier = new Identifier();
            pArgument.Result.FromStruct(identifier);
            result = new Model().SelectModelObject(identifier);
        }

        return result;
    }

    //
    // Summary:
    //     Combines two beams into one beam.
    //
    // Parameters:
    //   ObjectToCombineTo:
    //     The beam to be combined to.
    //
    //   ObjectToBeCombined:
    //     The beam which will be deleted after a successful operation.
    //
    // Returns:
    //     The combined beam on success, null on failure.
    public static Beam Combine(Beam ObjectToCombineTo, Beam ObjectToBeCombined)
    {
        Beam beam = new Beam();
        beam.Identifier = ObjectToCombineTo.Identifier;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Identifier.ToStruct(ObjectToCombineTo.Identifier);
        pArgument.Identifier2.ToStruct(ObjectToBeCombined.Identifier);
        pArgument.ManipulationType = 5;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) == 0 || !beam.Select())
        {
            beam = null;
        }

        return beam;
    }

    //
    // Summary:
    //     Combines two single rebars into one rebar.
    //
    // Parameters:
    //   ObjectToCombineTo:
    //     The rebar to be combined to.
    //
    //   ObjectToBeCombined:
    //     The rebar which will be deleted after a successful operation.
    //
    // Returns:
    //     The combined single rebar on success, null on failure.
    public static SingleRebar Combine(SingleRebar ObjectToCombineTo, SingleRebar ObjectToBeCombined)
    {
        SingleRebar singleRebar = new SingleRebar();
        singleRebar.Identifier = ObjectToCombineTo.Identifier;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Identifier.ToStruct(ObjectToCombineTo.Identifier);
        pArgument.Identifier2.ToStruct(ObjectToBeCombined.Identifier);
        pArgument.ManipulationType = 6;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) == 0 || !singleRebar.Select())
        {
            singleRebar = null;
        }

        return singleRebar;
    }

    //
    // Summary:
    //     Combines two rebar groups into one rebar group.
    //
    // Parameters:
    //   ObjectToCombineTo:
    //     The rebar group to be combined to.
    //
    //   ObjectToBeCombined:
    //     The rebar group which will be deleted after a successful operation.
    //
    // Returns:
    //     The combined rebar group on success, null on failure.
    public static RebarGroup Combine(RebarGroup ObjectToCombineTo, RebarGroup ObjectToBeCombined)
    {
        if (ObjectToCombineTo.StirrupType == RebarGroup.RebarGroupStirrupTypeEnum.STIRRUP_TYPE_TAPERED_CURVED)
        {
            throw new ArgumentException("This operation is not supported for Curved Reinforcements", "ObjectToCombineTo");
        }

        if (ObjectToBeCombined.StirrupType == RebarGroup.RebarGroupStirrupTypeEnum.STIRRUP_TYPE_TAPERED_CURVED)
        {
            throw new ArgumentException("This operation is not supported for Curved Reinforcements", "ObjectToBeCombined");
        }

        RebarGroup rebarGroup = new RebarGroup();
        rebarGroup.Identifier = ObjectToCombineTo.Identifier;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Identifier.ToStruct(ObjectToCombineTo.Identifier);
        pArgument.Identifier2.ToStruct(ObjectToBeCombined.Identifier);
        pArgument.ManipulationType = 6;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) == 0 || !rebarGroup.Select())
        {
            rebarGroup = null;
        }

        return rebarGroup;
    }

    //
    // Summary:
    //     Splits the beam and creates a new one in the given position.
    //
    // Parameters:
    //   Object:
    //     The beam object to be splitted.
    //
    //   SplitPoint:
    //     The position where splitting is executed.
    //
    // Returns:
    //     The created beam on success, null on failure.
    public static Beam Split(Beam Object, Point SplitPoint)
    {
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        Beam result = null;
        pArgument.Point1.ToStruct(SplitPoint);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 4;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            Identifier identifier = new Identifier();
            pArgument.Result.FromStruct(identifier);
            result = new Model().SelectModelObject(identifier) as Beam;
        }

        return result;
    }

    private static Reinforcement SplitRebar(Reinforcement Object, Line SplitLine)
    {
        Reinforcement result = null;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Point1.ToStruct(SplitLine.Origin);
        pArgument.Point2.ToStruct(SplitLine.Origin + SplitLine.Direction);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 4;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            Identifier identifier = new Identifier();
            pArgument.Result.FromStruct(identifier);
            result = new Model().SelectModelObject(identifier) as Reinforcement;
        }

        return result;
    }

    //
    // Summary:
    //     Splits the single rebar and creates a new one in the given position.
    //
    // Parameters:
    //   Object:
    //     The single rebar object to be splitted.
    //
    //   SplitLine:
    //     The line where splitting is executed.
    //
    // Returns:
    //     The created single rebar on success, null on failure.
    public static SingleRebar Split(SingleRebar Object, Line SplitLine)
    {
        return SplitRebar(Object, SplitLine) as SingleRebar;
    }

    //
    // Summary:
    //     Splits the curved rebar group and creates a new one in the given position.
    //
    // Parameters:
    //   Object:
    //     The curved rebar group object to be splitted.
    //
    //   SplitLine:
    //     The line where splitting is executed.
    //
    // Returns:
    //     The created curved rebar group on success, null on failure.
    public static CurvedRebarGroup Split(CurvedRebarGroup Object, Line SplitLine)
    {
        return SplitRebar(Object, SplitLine) as CurvedRebarGroup;
    }

    //
    // Summary:
    //     Splits the circle rebar group and creates a new one in the given position.
    //
    // Parameters:
    //   Object:
    //     The circle rebar group object to be splitted.
    //
    //   SplitLine:
    //     The line where splitting is executed.
    //
    // Returns:
    //     The created circle rebar group on success, null on failure.
    public static CircleRebarGroup Split(CircleRebarGroup Object, Line SplitLine)
    {
        return SplitRebar(Object, SplitLine) as CircleRebarGroup;
    }

    //
    // Summary:
    //     Splits the rebar group and creates a new one in the given position.
    //
    // Parameters:
    //   Object:
    //     The rebar group object to be splitted.
    //
    //   SplitLine:
    //     The line where splitting is executed.
    //
    // Returns:
    //     The created rebar group on success, null on failure.
    public static RebarGroup Split(RebarGroup Object, Line SplitLine)
    {
        return SplitRebar(Object, SplitLine) as RebarGroup;
    }

    //
    // Summary:
    //     Splits the contour plate and creates a new one along the given polygon.
    //
    // Parameters:
    //   Object:
    //     The contour plate object to be splitted.
    //
    //   SplitPolygon:
    //     The position where splitting is executed.
    //
    // Returns:
    //     The created contour plate on success, null on failure.
    public static ContourPlate Split(ContourPlate Object, Polygon SplitPolygon)
    {
        ContourPlate result = null;
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        SplitPolygon.ToStruct(ref pArgument.Polygon);
        pArgument.Identifier.ToStruct(Object.Identifier);
        pArgument.ManipulationType = 4;
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) != 0)
        {
            Identifier identifier = new Identifier();
            pArgument.Result.FromStruct(identifier);
            result = new Model().SelectModelObject(identifier) as ContourPlate;
        }

        return result;
    }

    //
    // Summary:
    //     This command is meant for specifically splitting a concrete slab with advanced
    //     solid operations to create more robust and user friendly results than the command:
    //     public static ContourPlate Split(ContourPlate Object, Polygon SplitPolygon).
    //     No validation is done for the type, it is the caller's responsibility to call
    //     this only for valid types (slabs). Behavior for non-slabs is undetermined.
    //
    // Parameters:
    //   PartId:
    //     The part ID that identifies the slab to be split
    //
    //   Polymesh:
    //     The polymesh that defines the splitting surface
    //
    // Returns:
    //     True on success.
    public static bool SplitSlab(int PartId, FacetedBrep Polymesh)
    {
        if (PartId < 1 || Polymesh == null)
        {
            return false;
        }

        dotPolymeshObject_t pSplit = default(dotPolymeshObject_t);
        pSplit.Polymesh.ClientId = dotClientId_t.GetClientId();
        Tekla.Structures.Model.Polymesh.ConvertToStruct(Polymesh, ref pSplit.Polymesh);
        return DelegateProxy.Delegate.ExportSplitPart(PartId, ref pSplit) != 0;
    }

    private static ModelObjectEnumerator UngroupReinforcement(Reinforcement Reinforcement)
    {
        if (Reinforcement == null)
        {
            throw new ArgumentNullException("Reinforcement");
        }

        if (!Reinforcement.Identifier.IsValid())
        {
            throw new ArgumentException("Reinforcement");
        }

        ArrayList arrayList = new ArrayList();
        ArrayList arrayList2 = new ArrayList();
        IntList intList = new IntList();
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        pArgument.Identifier.ToStruct(Reinforcement.Identifier);
        pArgument.ManipulationType = 7;
        pArgument.ClientID = dotClientId_t.GetClientId();
        if (DelegateProxy.Delegate.ExportManipulateObject(ref pArgument) > 0)
        {
            ListExporter.ImportIntList(intList);
        }

        foreach (int item in intList)
        {
            arrayList.Add(new Identifier(item));
            arrayList2.Add(ModelObject.ModelObjectEnum.SINGLEREBAR);
        }

        return new ModelObjectEnumerator(arrayList, arrayList2);
    }

    //
    // Summary:
    //     Ungroups the rebar group and creates new single rebars.
    //
    // Parameters:
    //   Reinforcement:
    //     The rebar group to be ungrouped.
    //
    // Returns:
    //     An enumerator of single rebars.
    public static ModelObjectEnumerator Ungrouping(RebarGroup Reinforcement)
    {
        return UngroupReinforcement(Reinforcement);
    }

    //
    // Summary:
    //     Ungroups the rebar mesh and creates new single rebars.
    //
    // Parameters:
    //   Reinforcement:
    //     The rebar mesh to be ungrouped.
    //
    // Returns:
    //     An enumerator of single rebars.
    public static ModelObjectEnumerator Ungrouping(RebarMesh Reinforcement)
    {
        return UngroupReinforcement(Reinforcement);
    }

    //
    // Summary:
    //     Groups a list of single rebars or rebar groups and creates a new rebar group.
    //
    //
    // Parameters:
    //   RebarList:
    //     The list of single rebars and rebar groups to be grouped.
    //
    // Returns:
    //     The created rebar group on success, null on failure.
    public static RebarGroup Group(IEnumerable RebarList)
    {
        if (RebarList == null)
        {
            throw new ArgumentNullException("RebarList");
        }

        int num = 0;
        IEnumerator enumerator = RebarList.GetEnumerator();
        IntList intList = new IntList();
        dotManipulateObject_t pArgument = default(dotManipulateObject_t);
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is Reinforcement reinforcement && reinforcement.Identifier.IsValid())
            {
                intList.Add(reinforcement.Identifier.ID);
            }
        }

        if (intList.Count > 0 && ListExporter.ExportIntList(intList))
        {
            pArgument = default(dotManipulateObject_t);
            pArgument.ManipulationType = 8;
            pArgument.ClientID = dotClientId_t.GetClientId();
            num = DelegateProxy.Delegate.ExportManipulateObject(ref pArgument);
        }

        RebarGroup rebarGroup = null;
        if (num > 0 && pArgument.Identifier.ID > 0)
        {
            rebarGroup = new RebarGroup();
            rebarGroup.Identifier.ID = pArgument.Identifier.ID;
            if (!rebarGroup.Select())
            {
                rebarGroup = null;
            }
        }

        return rebarGroup;
    }

    //
    // Summary:
    //     Show Only Selected objects in current view.
    //
    // Parameters:
    //   UnselectedMode:
    //     Specify what to do with unselected parts.
    public static void ShowOnlySelected(UnselectedModeEnum UnselectedMode)
    {
        switch (UnselectedMode)
        {
            case UnselectedModeEnum.Hidden:
                DelegateProxy.Delegate.ExportViewHideUnselected(HideCompletely: true, DrawAsStick: false);
                break;
            case UnselectedModeEnum.Transparent:
                DelegateProxy.Delegate.ExportViewHideUnselected(HideCompletely: false, DrawAsStick: false);
                break;
            case UnselectedModeEnum.AsSticks:
                DelegateProxy.Delegate.ExportViewHideUnselected(HideCompletely: false, DrawAsStick: true);
                break;
        }
    }

    //
    // Summary:
    //     Converts face vertices into a polygon for passing them to the TS core
    //
    // Parameters:
    //   facePolygon:
    //     Outputs an ordered polygon determining the face corner points
    //
    //   currentLoopPoints:
    //     The face loop points
    private static void FaceVerticesToPolygon(ref dotPolygon_t facePolygon, IList<Point> currentLoopPoints)
    {
        facePolygon.nPoints = currentLoopPoints.Count;
        for (int i = 0; i < currentLoopPoints.Count; i++)
        {
            facePolygon.aX[i] = currentLoopPoints[i].X;
            facePolygon.aY[i] = currentLoopPoints[i].Y;
            facePolygon.aZ[i] = currentLoopPoints[i].Z;
        }
    }

    //
    // Summary:
    //     Gets list of vertex points inside a solid face.
    //
    // Parameters:
    //   solidFace:
    //     Solid face to get points from.
    //
    // Returns:
    //     Returns list of vertexes inside a solid face.
    private static IList<Point> SolidFaceToPointList(Face solidFace)
    {
        IList<Point> list = new List<Point>();
        LoopEnumerator loopEnumerator = solidFace.GetLoopEnumerator();
        if (loopEnumerator.MoveNext())
        {
            VertexEnumerator vertexEnumerator = loopEnumerator.Current.GetVertexEnumerator();
            while (vertexEnumerator.MoveNext())
            {
                list.Add(vertexEnumerator.Current);
            }
        }

        return list;
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts.
    //     This method can change GUID when using from plug-ins. To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     One part used for creating the bent plate.
    //
    //   part2:
    //     The other part used for creating the bent plate.
    //
    //   bendShape:
    //     Shape of the bend (cylindrical or conical)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown if received unsupported bent plate creation input.
    public static BentPlate CreateBentPlateByParts(Part part1, Part part2, BentPlate.BendShape bendShape)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)DelegateProxy.Delegate.ExportCreateBentPlateByParts(part1.Identifier.ID, part2.Identifier.ID, (int)bendShape);
            if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
            {
                bentPlate = new BentPlate
                {
                    Identifier = part1.Identifier
                };
                if (!bentPlate.Select())
                {
                    bentPlate = null;
                }
            }
            else
            {
                BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
            }

            return bentPlate;
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a cylindrical bend that connects it to the
    //     second plate creating a new Tekla.Structures.Model.BentPlate instance based on
    //     two parts. See Tekla.Structures.Model.Operations.Operation.CreateBentPlateByParts(Tekla.Structures.Model.Part,Tekla.Structures.Model.Part,Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     One part used for creating the bent plate.
    //
    //   part2:
    //     The other part used for creating the bent plate.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    public static BentPlate CreateBentPlateByParts(Part part1, Part part2)
    {
        return CreateBentPlateByParts(part1, part2, BentPlate.BendShape.Cylindrical);
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts and
    //     a radius. This method can change GUID when using from plug-ins. To keep GUID,
    //     use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Model.ConnectiveGeometry,System.Double).
    //
    //
    // Parameters:
    //   part1:
    //     One part used for creating the bent plate.
    //
    //   part2:
    //     The other part used for creating the bent plate.
    //
    //   radius:
    //     The target radius for the created cylindrical section.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown if received unsupported bent plate creation input.
    public static BentPlate CreateBentPlateByParts(Part part1, Part part2, double radius)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)DelegateProxy.Delegate.ExportCreateBentPlateByPartsAndRadius(part1.Identifier.ID, part2.Identifier.ID, radius);
            if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
            {
                bentPlate = new BentPlate
                {
                    Identifier = part1.Identifier
                };
                if (!bentPlate.Select())
                {
                    bentPlate = null;
                }
            }
            else
            {
                BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
            }

            return bentPlate;
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a conical bend that connects it to the second
    //     plate creating a new Tekla.Structures.Model.BentPlate instance based on two parts.
    //     The resulting bend will have the given aperture and the provided larger radius.
    //
    //
    // Parameters:
    //   part1:
    //     One part used for creating the bent plate.
    //
    //   part2:
    //     The other part used for creating the bent plate.
    //
    //   largestRadius:
    //     Radius of the largest section of the cone
    //
    //   halfAperture:
    //     Angle between a generatrix of the cone and its center line (i.e. axis)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise
    public static BentPlate CreateConicalBentPlateByPartsAndAperture(Part part1, Part part2, double largestRadius, double halfAperture)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)DelegateProxy.Delegate.ExportCreateConicalBentPlateByPartsAndRadiusAperture(part1.Identifier.ID, part2.Identifier.ID, largestRadius, halfAperture);
            if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
            {
                bentPlate = new BentPlate
                {
                    Identifier = part1.Identifier
                };
                if (!bentPlate.Select())
                {
                    bentPlate = null;
                }
            }
            else
            {
                BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
            }

            return bentPlate;
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a conical bend that connects it to the second
    //     plate creating a new Tekla.Structures.Model.BentPlate instance based on two parts.
    //     The resulting bend will have the two given radiuses.
    //
    // Parameters:
    //   part1:
    //     One part used for creating the bent plate.
    //
    //   part2:
    //     The other part used for creating the bent plate.
    //
    //   firstRadius:
    //     Radius of one section of the cone
    //
    //   secondRadius:
    //     Radius of the other section of the cone
    //
    // Returns:
    //     The bent plate object if successful, null otherwise
    public static BentPlate CreateConicalBentPlateByPartsAndTwoRadiuses(Part part1, Part part2, double firstRadius, double secondRadius)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)DelegateProxy.Delegate.ExportCreateConicalBentPlateByPartsAndTwoRadiuses(part1.Identifier.ID, part2.Identifier.ID, firstRadius, secondRadius);
            if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
            {
                bentPlate = new BentPlate
                {
                    Identifier = part1.Identifier
                };
                if (!bentPlate.Select())
                {
                    bentPlate = null;
                }
            }
            else
            {
                BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
            }

            return bentPlate;
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts and
    //     selected faces in each part. This method can change GUID when using from plug-ins.
    //     To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected face on the second part.
    //
    //   bendShape:
    //     Shape of the bend (cylindrical or conical)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateBentPlateByFaces(Part part1, IList<Point> face1, Part part2, IList<Point> face2, BentPlate.BendShape bendShape)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            if (face1.Count == 4 && face2.Count == 4)
            {
                dotPolygon_t facePolygon = new dotPolygon_t(99);
                dotPolygon_t facePolygon2 = new dotPolygon_t(99);
                FaceVerticesToPolygon(ref facePolygon, face1);
                FaceVerticesToPolygon(ref facePolygon2, face2);
                ICDelegate @delegate = DelegateProxy.Delegate;
                int iD = part1.Identifier.ID;
                int iD2 = part2.Identifier.ID;
                BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)@delegate.ExportCreateBentPlateByFaces(iD, iD2, ref facePolygon, ref facePolygon2, (int)bendShape);
                if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
                {
                    bentPlate = new BentPlate
                    {
                        Identifier = new Identifier(iD)
                    };
                    if (!bentPlate.Select())
                    {
                        bentPlate = null;
                    }
                }
                else
                {
                    BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
                }

                return bentPlate;
            }

            throw new ArgumentException("The selected face has a number of corner points other than four. The selected side faces must have 4 vertices.");
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a cylindrical bend that connects it to the
    //     second plate creating a new Tekla.Structures.Model.BentPlate instance based on
    //     two parts and selected faces in each part. See Tekla.Structures.Model.Operations.Operation.CreateBentPlateByFaces(Tekla.Structures.Model.Part,System.Collections.Generic.IList{Tekla.Structures.Geometry3d.Point},Tekla.Structures.Model.Part,System.Collections.Generic.IList{Tekla.Structures.Geometry3d.Point},Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected face on the second part.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    public static BentPlate CreateBentPlateByFaces(Part part1, IList<Point> face1, Part part2, IList<Point> face2)
    {
        return CreateBentPlateByFaces(part1, face1, part2, face2, BentPlate.BendShape.Cylindrical);
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts and
    //     selected faces in each part. This method can change GUID when using from plug-ins.
    //     To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected solid face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected solid face on the second part.
    //
    //   bendShape:
    //     Shape of the bend (cylindrical or conical)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateBentPlateByFaces(Part part1, Face face1, Part part2, Face face2, BentPlate.BendShape bendShape)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null && face1 != null && face2 != null)
        {
            IList<Point> face3 = SolidFaceToPointList(face1);
            IList<Point> face4 = SolidFaceToPointList(face2);
            return CreateBentPlateByFaces(part1, face3, part2, face4, bendShape);
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a cylindrical bend that connects it to the
    //     second plate creating a new Tekla.Structures.Model.BentPlate instance based on
    //     two parts and selected faces in each part. See Tekla.Structures.Model.Operations.Operation.CreateBentPlateByFaces(Tekla.Structures.Model.Part,Tekla.Structures.Solid.Face,Tekla.Structures.Model.Part,Tekla.Structures.Solid.Face,Tekla.Structures.Model.BentPlate.BendShape).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected face on the second part.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    public static BentPlate CreateBentPlateByFaces(Part part1, Face face1, Part part2, Face face2)
    {
        return CreateBentPlateByFaces(part1, face1, part2, face2, BentPlate.BendShape.Cylindrical);
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts,
    //     selected faces in each part and radius. This method can change GUID when using
    //     from plug-ins. To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,System.Double).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected face on the second part.
    //
    //   radius:
    //     The target radius for the created cylindrical section.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateBentPlateByFaces(Part part1, IList<Point> face1, Part part2, IList<Point> face2, double radius)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            if (face1.Count == 4 && face2.Count == 4)
            {
                dotPolygon_t facePolygon = new dotPolygon_t(99);
                dotPolygon_t facePolygon2 = new dotPolygon_t(99);
                FaceVerticesToPolygon(ref facePolygon, face1);
                FaceVerticesToPolygon(ref facePolygon2, face2);
                ICDelegate @delegate = DelegateProxy.Delegate;
                int iD = part1.Identifier.ID;
                int iD2 = part2.Identifier.ID;
                BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)@delegate.ExportCreateBentPlateByFacesAndRadius(iD, iD2, ref facePolygon, ref facePolygon2, radius);
                if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
                {
                    bentPlate = new BentPlate
                    {
                        Identifier = new Identifier(iD)
                    };
                    if (!bentPlate.Select())
                    {
                        bentPlate = null;
                    }
                }
                else
                {
                    BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
                }

                return bentPlate;
            }

            throw new ArgumentException("The selected face has a number of corner points other than four. The selected side faces must have 4 vertices.");
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a bend that connects it to the second plate
    //     creating a new Tekla.Structures.Model.BentPlate instance based on two parts and
    //     selected faces in each part and radius. This method can change GUID when using
    //     from plug-ins. To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,System.Double).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected solid face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected solid face on the second part.
    //
    //   radius:
    //     The target radius for the created cylindrical section.
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateBentPlateByFaces(Part part1, Face face1, Part part2, Face face2, double radius)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null && face1 != null && face2 != null)
        {
            IList<Point> face3 = SolidFaceToPointList(face1);
            IList<Point> face4 = SolidFaceToPointList(face2);
            return CreateBentPlateByFaces(part1, face3, part2, face4, radius);
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a conical bend that connects it to the second
    //     plate creating a new Tekla.Structures.Model.BentPlate instance based on two parts,
    //     selected faces in each part and radius. This method can change GUID when using
    //     from plug-ins. To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,System.Double).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected face on the second part.
    //
    //   largestRadius:
    //     The largest target radius for the created conical section.
    //
    //   halfAperture:
    //     Angle between a generatrix of the cone and its center line (i.e. axis)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateConicalBentPlateByFaces(Part part1, IList<Point> face1, Part part2, IList<Point> face2, double largestRadius, double halfAperture)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null)
        {
            if (face1.Count == 4 && face2.Count == 4)
            {
                dotPolygon_t facePolygon = new dotPolygon_t(99);
                dotPolygon_t facePolygon2 = new dotPolygon_t(99);
                FaceVerticesToPolygon(ref facePolygon, face1);
                FaceVerticesToPolygon(ref facePolygon2, face2);
                ICDelegate @delegate = DelegateProxy.Delegate;
                int iD = part1.Identifier.ID;
                int iD2 = part2.Identifier.ID;
                BentPlateGeometrySolver.OperationStatus operationStatus = (BentPlateGeometrySolver.OperationStatus)@delegate.ExportCreateConicalBentPlateByFacesAndRadiusAperture(iD, iD2, ref facePolygon, ref facePolygon2, largestRadius, halfAperture);
                if (operationStatus == BentPlateGeometrySolver.OperationStatus.Success)
                {
                    bentPlate = new BentPlate
                    {
                        Identifier = new Identifier(iD)
                    };
                    if (!bentPlate.Select())
                    {
                        bentPlate = null;
                    }
                }
                else
                {
                    BentPlateGeometrySolver.ThrowConnectiveGeometryException(operationStatus);
                }

                return bentPlate;
            }

            throw new ArgumentException("The selected face has a number of corner points other than four. The selected side faces must have 4 vertices.");
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Modifies the first plate by adding a conical bend that connects it to the second
    //     plate creating a new Tekla.Structures.Model.BentPlate instance based on two parts
    //     and selected faces in each part, and the largest radius of the conical section
    //     and the cone aperture. This method can change GUID when using from plug-ins.
    //     To keep GUID, use Tekla.Structures.Model.BentPlateGeometrySolver.AddLeg(Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,Tekla.Structures.Model.ConnectiveGeometry,Tekla.Structures.Geometry3d.LineSegment,System.Double).
    //
    //
    // Parameters:
    //   part1:
    //     The first part.
    //
    //   face1:
    //     The selected solid face on the first part.
    //
    //   part2:
    //     The second part.
    //
    //   face2:
    //     The selected solid face on the second part.
    //
    //   largestRadius:
    //     Largest radius of the conical section
    //
    //   halfAperture:
    //     Angle between a generatrix of the cone and its center line (i.e. axis)
    //
    // Returns:
    //     The bent plate object if successful, null otherwise.
    //
    // Exceptions:
    //   T:Tekla.Structures.Model.ConnectiveGeometryException:
    //     Thrown if could not create ConnectiveGeometry instance.
    //
    //   T:System.ArgumentException:
    //     Thrown when faces have incorrect number of face points or received unsupported
    //     bent plate creation input.
    public static BentPlate CreateConicalBentPlateByFaces(Part part1, Face face1, Part part2, Face face2, double largestRadius, double halfAperture)
    {
        BentPlate bentPlate = null;
        if (part1 != null && part2 != null && face1 != null && face2 != null)
        {
            IList<Point> face3 = SolidFaceToPointList(face1);
            IList<Point> face4 = SolidFaceToPointList(face2);
            return CreateConicalBentPlateByFaces(part1, face3, part2, face4, largestRadius, halfAperture);
        }

        throw new ArgumentException("Unsupported bent plate creation input");
    }

    //
    // Summary:
    //     Deletes bentPlate and inserts Tekla.Structures.Model.ContourPlates instances
    //     equivalent to the ones used to create bentPlate. The Tekla.Structures.Model.ContourPlate
    //     created from the main polygon has the same identifier as bentPlate.
    //
    // Parameters:
    //   bentPlate:
    //     the Tekla.Structures.Model.BentPlate instance to explode.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when bentPlate is invalid.
    public static bool ExplodeBentPlate(BentPlate bentPlate)
    {
        if (!bentPlate.Identifier.IsValid())
        {
            throw new ArgumentException("Identifier not valid");
        }

        ICDelegate @delegate = DelegateProxy.Delegate;
        int iD = bentPlate.Identifier.ID;
        return Convert.ToBoolean(@delegate.ExplodeBentPlate(iD));
    }

    internal static bool IsAcceptedTypeForPourUnit(ModelObject modelObject)
    {
        switch (TypeMapper.MapTypesToIntList(new Type[1] { modelObject.GetType() })[0])
        {
            case ModelObject.ModelObjectEnum.ASSEMBLY:
            case ModelObject.ModelObjectEnum.REBARGROUP:
            case ModelObject.ModelObjectEnum.REBARMESH:
            case ModelObject.ModelObjectEnum.BOLT_ARRAY:
            case ModelObject.ModelObjectEnum.REBAR_SET:
                return true;
            default:
                return false;
        }
    }

    //
    // Summary:
    //     Adds model objects as part of a pour unit Model object types accepted are assembly
    //     types except cast in situ, reinforcements of different kind, components and bolts
    //
    //
    // Parameters:
    //   inputPourUnit:
    //     the instance of pour unit to add objects to.
    //
    //   objectsToBeAdded:
    //     the list of model objects to be added.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Throw exceptions if Pour or object has invalid ID, do not exist in model or not
    //     valid object type.
    public static bool AddToPourUnit(PourUnit inputPourUnit, List<ModelObject> objectsToBeAdded)
    {
        if (!inputPourUnit.Select())
        {
            throw new ArgumentException("The value passed for the inputPour is invalid");
        }

        StringList stringList = new StringList();
        List<string> list = new List<string>();
        foreach (ModelObject item in objectsToBeAdded)
        {
            if (!item.Identifier.IsValid() || !IsAcceptedTypeForPourUnit(item))
            {
                list.Add(item.Identifier.GUID.ToString().ToUpper());
            }
            else
            {
                stringList.Add(item.Identifier.GUID.ToString().ToUpper());
            }
        }

        if (list.Count > 0)
        {
            throw new ArgumentException("The following object IDs are not accepted and were not added to pour: " + string.Join(" ", list));
        }

        dotPourUnit_t PourUnit = default(dotPourUnit_t);
        inputPourUnit.ToStruct(ref PourUnit);
        ListExporter.ExportStringList(stringList);
        dotClientId_t clientId = dotClientId_t.GetClientId();
        return DelegateProxy.Delegate.ExportAddToPourUnit(ref PourUnit, ref clientId);
    }

    //
    // Summary:
    //     Removes model object from pour unit Model object types accepted are assembly
    //     types except cast in situ, reinforcements of different kind, components and bolts
    //
    //
    // Parameters:
    //   objectsToBeRemoved:
    //     the list of model objects to be added.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Throw exceptions if object has invalid ID, do not exist in model or not valid
    //     object type.
    public static bool RemoveFromPourUnit(List<ModelObject> objectsToBeRemoved)
    {
        List<string> list = new List<string>();
        StringList stringList = new StringList();
        foreach (ModelObject item in objectsToBeRemoved)
        {
            if (!item.Identifier.IsValid() || !IsAcceptedTypeForPourUnit(item))
            {
                list.Add(item.Identifier.GUID.ToString().ToUpper());
            }
            else
            {
                stringList.Add(item.Identifier.GUID.ToString().ToUpper());
            }
        }

        if (list.Count > 0)
        {
            throw new ArgumentException("The following object IDs are not accepted and were not added to pour: " + string.Join(" ", list));
        }

        ICDelegate @delegate = DelegateProxy.Delegate;
        ListExporter.ExportStringList(stringList);
        dotClientId_t clientId = dotClientId_t.GetClientId();
        return @delegate.ExportRemoveFromPourUnit(ref clientId);
    }

    //
    // Summary:
    //     Calculate and assign objects to pour unit Model object types that are associated
    //     with pour unit are assembly types except cast in situ, reinforcements of different
    //     kind, components and screws
    public static bool CalculatePourUnits()
    {
        return DelegateProxy.Delegate.ExportCalculatePourUnits();
    }

    //
    // Summary:
    //     Get the Shape Item Handle Points
    //
    // Parameters:
    //   guid:
    //     The guid of the shape
    //
    // Returns:
    //     List of Handle Points
    public static List<Point> GetHandlePoints(string guid)
    {
        List<Point> handlePoints = new List<Point>();
        if (string.IsNullOrEmpty(guid))
        {
            return handlePoints;
        }

        dotClientId_t clientId = dotClientId_t.GetClientId();
        DelegateProxy.Delegate.ExportGetHandlePoints(clientId, guid, ref handlePoints);
        return handlePoints;
    }

    //
    // Summary:
    //     Set the Shape Item Handle Points
    //
    // Parameters:
    //   guid:
    //     The guid of the shape
    //
    //   HandlePoints:
    //     An array of handle points
    //
    // Returns:
    //     True if the appending operation was successful
    public static bool SetHandlePoints(string guid, List<Point> HandlePoints)
    {
        if (string.IsNullOrEmpty(guid) || HandlePoints == null)
        {
            return false;
        }

        dotClientId_t clientId = dotClientId_t.GetClientId();
        PointList pointList = new PointList();
        foreach (Point HandlePoint in HandlePoints)
        {
            pointList.Add(HandlePoint);
        }

        ListExporter.ExportPointList(pointList);
        return DelegateProxy.Delegate.ExportSetHandlePoints(ref clientId, guid);
    }

    //
    // Summary:
    //     Displays a message in the status bar.
    //
    // Parameters:
    //   Message:
    //     The message to display.
    //
    // Returns:
    //     True if the message could be displayed.
    //
    // Exceptions:
    //   T:System.ArgumentNullException:
    //     Thrown when the Message is null.
    //
    // Remarks:
    //     Prompts
    //
    //     Tekla Structures prefixes the given prompt with "prompt_" and looks for a translation
    //     in the prompts.ail file. If the translation (e.g. "prompt_Pick_first_position")
    //     is not found in the prompts.ail file, the prompt string is displayed as such.
    //     This feature can be used to give already translated strings to the picker.
    public static bool DisplayPrompt(string Message)
    {
        bool result = true;
        if (Message != null)
        {
            if (DelegateProxy.Delegate.ExportDisplayPrompt(Message) <= 0)
            {
                result = false;
            }

            return result;
        }

        throw new ArgumentNullException("Invalid message.");
    }

    //
    // Summary:
    //     Checks whether the object matches to the criteria in the given filter.
    //
    // Parameters:
    //   ModelObject:
    //     The model object to check.
    //
    //   FilterName:
    //     The filter file to check against.
    //
    // Returns:
    //     True if the object matches to the given filter criteria.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the ModelObject is not valid.
    public static bool ObjectMatchesToFilter(ModelObject ModelObject, string FilterName)
    {
        bool result = false;
        if (ModelObject == null)
        {
            throw new ArgumentNullException("ModelObject");
        }

        if (!ModelObject.Identifier.IsValid())
        {
            throw new ArgumentException("Identifier not valid");
        }

        dotIdentifier_t pObjectId = default(dotIdentifier_t);
        pObjectId.ToStruct(ModelObject.Identifier);
        if (DelegateProxy.Delegate.ExportObjectMatchesToFilter(ref pObjectId, FilterName) > 0)
        {
            result = true;
        }

        return result;
    }

    //
    // Summary:
    //     Checks whether the object matches to the criteria in the given filter.
    //
    // Parameters:
    //   ModelObject:
    //     The model object to check.
    //
    //   FilterExpression:
    //     The definition of a selection filter to check against.
    //
    // Returns:
    //     True if the object matches to the given filter criteria.
    //
    // Exceptions:
    //   T:System.ArgumentException:
    //     Thrown when the ModelObject is not valid.
    public static bool ObjectMatchesToFilter(ModelObject ModelObject, FilterExpression FilterExpression)
    {
        string fullFileName = Path.Combine(Path.Combine(new Model().GetInfo().ModelPath, "attributes"), "GetObjectsByFilterTempFilter");
        fullFileName = new Filter(FilterExpression).CreateFile(FilterExpressionFileType.OBJECT_GROUP_SELECTION, fullFileName);
        bool result = ObjectMatchesToFilter(ModelObject, "GetObjectsByFilterTempFilter.SObjGrp");
        if (File.Exists(fullFileName))
        {
            File.Delete(fullFileName);
        }

        return result;
    }
}
#if false // Decompilation log
'23' items in cache
------------------
Resolve: 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll'
------------------
Resolve: 'System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll'
------------------
Resolve: 'Tekla.Structures, Version=2021.0.0.0, Culture=neutral, PublicKeyToken=2f04dbe497b71114'
Found single assembly: 'Tekla.Structures, Version=2021.0.0.0, Culture=neutral, PublicKeyToken=2f04dbe497b71114'
Load from: 'C:\Users\abc00\.nuget\packages\tekla.structures\2021.0.0\lib\net40\Tekla.Structures.dll'
------------------
Resolve: 'System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll'
------------------
Resolve: 'Akit5, Version=5.8.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
Could not find by name: 'Akit5, Version=5.8.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
------------------
Resolve: 'Newtonsoft.Json, Version=12.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed'
Found single assembly: 'Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed'
WARN: Version mismatch. Expected: '12.0.0.0', Got: '13.0.0.0'
Load from: 'C:\Users\abc00\.nuget\packages\newtonsoft.json\13.0.3\lib\net45\Newtonsoft.Json.dll'
------------------
Resolve: 'Tekla.Technology.Serialization, Version=1.0.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
Could not find by name: 'Tekla.Technology.Serialization, Version=1.0.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
------------------
Resolve: 'Tekla.Common.Geometry.Primitives, Version=4.5.0.0, Culture=neutral, PublicKeyToken=93c0f7e4f1ca9619'
Could not find by name: 'Tekla.Common.Geometry.Primitives, Version=4.5.0.0, Culture=neutral, PublicKeyToken=93c0f7e4f1ca9619'
------------------
Resolve: 'System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Drawing.dll'
------------------
Resolve: 'Tekla.Structures.Datatype, Version=2021.0.0.0, Culture=neutral, PublicKeyToken=2f04dbe497b71114'
Found single assembly: 'Tekla.Structures.Datatype, Version=2021.0.0.0, Culture=neutral, PublicKeyToken=2f04dbe497b71114'
Load from: 'C:\Users\abc00\.nuget\packages\tekla.structures.datatype\2021.0.0\lib\net40\Tekla.Structures.Datatype.dll'
------------------
Resolve: 'Tekla.Technology.Scripting, Version=5.0.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
Could not find by name: 'Tekla.Technology.Scripting, Version=5.0.0.0, Culture=neutral, PublicKeyToken=a70cba4ef557ee03'
#endif
