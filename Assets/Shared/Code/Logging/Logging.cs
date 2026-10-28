using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Vox;
using OGD;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SpaceFab;
using System.Diagnostics.Tracing;
using FieldDay.Analytics;
using System.Linq;
using SpaceFab.Design;
using Debug = UnityEngine.Debug;
using SpaceFab.Overarching;


namespace SpaceFab.Logging
{
    public class Logging : MonoBehaviour
    {
        public enum VolumeType: byte
        {
            MASTER,
            MUSIC,
            SFX
        }

        public enum ToolID : byte
        {
            PTYPE,
            NTYPE,
            METAL,
            CONTACT,
            GATE
        }

        public enum Minigame: byte
        {
            RESEARCH,
            DESIGN,
            SUPPLY_CHAIN,
            FABRICATION
        }

        public enum Layer: byte
        {
            METAL,
            SILICON
        }

        public enum ElementState: byte
        {
            HI,
            LO
        }

        public enum Station: byte
        {
            FURNACE,
            SPIN_COATER,
            MASK_ALIGNER,
            ETCHER,
            ION_IMPLANTER,
            SPUTTERER
        }

        public enum FabricationLayer: byte
        {
            METAL,
            PTYPE,
            NTYPE
        }

        public struct DesignIO
        {
            public List<GridCoord> Inputs;
            public List<GridCoord> Outputs;
        }

        public struct DesignGrid
        {
            public List<List<HashSet<ToolID>>> Grid;
        }

        // public  List<List<HashSet<ToolID>>> DesignGrid;

        private const ushort CLIENT_LOG_VERSION = 0;
        private readonly JsonBuilder m_JsonBuilder = new JsonBuilder(Unsafe.KiB * 64); // json allocation capacity
        private OGDLog m_Log;
        [NonSerialized] private bool m_Debug;

        #region Inspector
        [SerializeField, Required] private string m_AppId = "SPACEFAB";
        [SerializeField, Required] private string m_AppVersion;
        [SerializeField] private FirebaseConsts m_Firebase;
        [SerializeField] private bool m_Testing;

        [NonSerialized] private float m_SecondsFromLaunch;
        [NonSerialized] private string m_CurrentContract;
        [NonSerialized] private Minigame? m_CurrentMinigame;
        [NonSerialized] private Dictionary<string, Dictionary<Minigame, int>> m_ContractLevels = new Dictionary<string, Dictionary<Minigame, int>>(); // number of levels of each minigame, specified by the contract
        [NonSerialized] private DesignGrid? m_DesignGrid = null;

        #endregion //Inspector

        private void Start()
        {
            RegisterEvents();
            PrepareLogging();
        }

        #region Game State

        private void SubmitGameState()
        {
            m_JsonBuilder.Clear();

            // TODO
            m_JsonBuilder.Begin()
                .Field("seconds_from_launch", m_SecondsFromLaunch)
                .Field("current_contract", m_CurrentContract)
                .Field("current_minigame", m_CurrentMinigame.HasValue ? m_CurrentMinigame.Value.ToString() : null);
            
            m_JsonBuilder.BeginObject("contract_levels"); // key: contract
            if (m_ContractLevels != null)
            {
                foreach (var kvp in m_ContractLevels)
                {
                    m_JsonBuilder.BeginObject(kvp.Key); // key: minigame
                    foreach (var minigameKvp in kvp.Value)
                    {
                        m_JsonBuilder.Field(minigameKvp.Key.ToString(), minigameKvp.Value); // value: number of levels
                    }
                    m_JsonBuilder.EndObject();
                }
            }
            m_JsonBuilder.EndObject();

            if (m_CurrentMinigame == Minigame.DESIGN)
            {
                if (m_DesignGrid.HasValue)
                {
                    m_JsonBuilder.BeginArray("design_grid");
                    m_DesignGrid.Value.Grid.ForEach(row => row.ForEach(cell =>
                    {
                        m_JsonBuilder.BeginArray();
                        foreach (var toolId in cell)
                        {
                            m_JsonBuilder.Item(toolId.ToString());
                        }
                        m_JsonBuilder.EndArray();
                    }));
                    m_JsonBuilder.EndArray();
                }
                else
                {
                    m_JsonBuilder.Field("design_grid", (string)null);
                }

            }
            else
            {
                m_JsonBuilder.BeginArray("design_grid");
                m_JsonBuilder.EndArray();
            }
            m_Log.GameState(m_JsonBuilder.End());
        }

        private void UpdateContractLevels(string contract, Minigame minigame, int numLevel)
        {
            m_ContractLevels[contract][minigame] += numLevel ;
        }

        private void UpdateContractId(string contract)
        {
            m_CurrentContract = contract;
        }

        private void UpdateDesignGridState(Vector2Int gridPos, string tool)
        {
            //if (m_CurrentMinigame != Minigame.DESIGN)
            //{
            //    m_DesignGrid = null;
            //    return;
            //}
            // initialize design grid for horizontal slice
            if (!m_DesignGrid.HasValue)
            {
                var grid = new List<List<HashSet<ToolID>>>(DesignConsts.NUM_GRID_ROWS);
                for (int i = 0; i < DesignConsts.NUM_GRID_ROWS; i++)
                {
                    var row = new List<HashSet<ToolID>>(DesignConsts.NUM_GRID_COLS);
                    for (int j = 0; j < DesignConsts.NUM_GRID_COLS; j++)
                    {
                        row.Add(new HashSet<ToolID>());
                    }
                    grid.Add(row);
                }

                m_DesignGrid = new DesignGrid() { Grid = grid };
            }

            switch (tool)
            {
                case "ClearAll":
                    for (int i = 0; i < DesignConsts.NUM_GRID_ROWS; i++)
                    {
                        for (int j = 0; j < DesignConsts.NUM_GRID_COLS; j++)
                        {
                            m_DesignGrid.Value.Grid[i][j].Clear();
                        }
                    } 
                    break;
                case "Erase":
                    m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Clear();
                    break;
                case "Metal":
                    if (!m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Contains(ToolID.METAL))
                    {
                        m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Add(ToolID.METAL);
                    }
                    break;
                case "PTransistor":
                    if (!m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Contains(ToolID.PTYPE))
                    {
                        m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Add(ToolID.PTYPE);
                    }
                    break;
                case "NTransistor":
                    if (!m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Contains(ToolID.NTYPE))
                    {
                        m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Add(ToolID.NTYPE);
                    }
                    break;
                case "Via":
                    if (!m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Contains(ToolID.CONTACT))
                    {
                        m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Add(ToolID.CONTACT);
                    }
                    break;
                case "Gate":
                case "GateAbove":
                case "GateBelow":
                    if (!m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Contains(ToolID.GATE))
                    {
                        m_DesignGrid.Value.Grid[gridPos.x][gridPos.y].Add(ToolID.GATE);
                    }
                    break;
                case "Implicit":
                case "NONE":
                case "Input":
                case "Output":
                    break;
                default:
                    throw new ArgumentException($"Unrecognized tool: {tool}");
            }
            SubmitGameState();
        }


        #endregion // Game State

        #region Initialization

        private void PrepareLogging()
        {
#if DEVELOPMENT
            m_Debug = true;
#endif // DEVELOPMENT

            m_Log = new OGDLog(CreateOGDConsts(), new OGDLog.MemoryConfig(2048 * 2, Unsafe.KiB * 64, 256));

            if (!string.IsNullOrEmpty(m_Firebase.ApiKey))
            {
                m_Log.UseFirebase(m_Firebase);
            }
            m_Log.SetDebug(m_Debug);

            // "testing mode" in editor: skip OGD upload but disable debug
#if UNITY_EDITOR
            if (!m_Testing)
            {
                m_Log.AddSettings(OGDLog.SettingsFlags.SkipOGDUpload);
                m_Log.SetDebug(false);
            }
#endif //UNITY_EDITOR

            OGDLog.SchedulingConfig sched = OGDLog.SchedulingConfig.Default;
            sched.FlushDelay = 2;
            m_Log.ConfigureScheduling(sched);
        }

        private void SetAnalyticsUserCode(string userCode)
        {
            Log.Msg("[OGDLog] Assigning user code {0}", userCode);
            m_Log.SetUserId(userCode);
            m_Log.NewEvent("session_start");
            m_Log.Initialize(CreateOGDConsts());
        }

        private OGDLogConsts CreateOGDConsts()
        {
            return new OGDLogConsts()
            {
                AppId = m_AppId,
                AppVersion = m_AppVersion,
                AppBranch = BuildInfo.Branch(),
                ClientLogVersion = CLIENT_LOG_VERSION,
            };
        }

        #endregion //Initialization

        #region Event Registration
        private void RegisterEvents()
        {
            SpacefabGame.Events.Register<string>(GameEvents.TitleProfileStarting, SetAnalyticsUserCode);

            // TODO: Logging events
            SpacefabGame.Events
                .Register<bool>(GameEvents.TitleStartGameClicked, LogGameStart)
                .Register(GameEvents.TitleNewGameClicked, LogClickNewGame)
                .Register(GameEvents.TitleContinueGameClicked, LogClickResumeGame)
                .Register(GameEvents.ShipMenuDisplayed, LogShipMenuDisplayed)
                .Register<MinigameId>(GameEvents.SelectMinigame, HandleMinigameSelect)
                .Register<MinigameId>(GameEvents.StartMinigame, HandleMinigameStart)
                .Register(GameEvents.OnMinigameExit, HandleMinigameExit);

            // Contrat
            SpacefabGame.Events
                .Register(GameEvents.OpenContractView, LogOpenContractView)
                .Register<string>(GameEvents.AcceptContract, LogAcceptContract)
                .Register<string>(GameEvents.SelectContract, UpdateContractId)
                .Register<string>(GameEvents.StartChangeContract, LogStartChangeContract)
                .Register<string>(GameEvents.ConfirmSelectContract, LogConfirmSelectContract)
                .Register<string>(GameEvents.CancelSelectContract, LogCancelSelectContract);

            // Design
            SpacefabGame.Events
                .Register<GridStackConfig>(GameEvents.DeisgnGridSetup, LogDesignLevelBegin)
                .Register<ToolType>(GameEvents.DesignToolSelected, LogSelectTool)
                .Register<(GridCoord, string)>(GameEvents.DesignGridModified, (data) => LogFillGrid(data.Item1, data.Item2))
                .Register<(Vector2Int, string)>(GameEvents.DesignGridCleared, (data) => UpdateDesignGridState(data.Item1, data.Item2))
                .Register<(List<GridCoord>, List<GridCoord>, int)>(GameEvents.DesignSimRowStarted, (data) => LogStartTest(data.Item1, data.Item2, data.Item3))
                .Register<(string, int)>(GameEvents.DesignSimRowResolved, (data) => LogTestRowResolved(data.Item1, data.Item2))
                .Register(GameEvents.DesignSimSuiteSucceeded, LogDesignComplete);

            // Fabrication
            SpacefabGame.Events
                .Register(GameEvents.FabGenerateWafer, LogGenerateWafer)
                .Register(GameEvents.FabTimeStart, LogTimerStart)
                .Register<(string, bool)>(GameEvents.FabInstructionUpdated, (data) => LogInstructionUpdated(data.Item1, data.Item2))
                .Register(GameEvents.FabStationEnterBegin, (string stationId) => LogActivateStation(stationId))
                .Register<(string, string)>(GameEvents.FabWrongStationAttempt, LogInvalidActivation)
                .Register<(string, float, bool)>(GameEvents.FabMicrogameCompleted, (data) => LogStationComplete(data.Item1, data.Item2, data.Item3))
                .Register(GameEvents.FabCompleted, LogFabricationComplete)
                .Register<(float, float, int)>(GameEvents.FabSucceeded, (data) => LogFabricationSuccess(data.Item1, data.Item2, data.Item3));

        }
        #endregion

        #region Logging Variables

        //[NonSerialized] private string m_CurrentContractId;

        #endregion // Logging Variables

        #region State Handlers
        private ToolID ConvertToToolID(GridCellConfig cellConfig)
        {
            if (cellConfig.CellType != CellType.NONE)
            {
                switch (cellConfig.CellType)
                {
                    case CellType.Metal:
                        return ToolID.METAL;
                    case CellType.PTransistor:
                        return ToolID.PTYPE;
                    case CellType.NTransistor:
                        return ToolID.NTYPE;
                    default:
                        throw new ArgumentException($"Unrecognized CellType: {cellConfig.CellType}");
                }
            }
            else
            {
                switch(cellConfig.TransferType)
                {
                    case TransferType.Via:
                        return ToolID.CONTACT;
                    case TransferType.GateAbove:
                    case TransferType.GateBelow:
                        return ToolID.GATE;
                    default:
                        throw new ArgumentException($"Unrecognized TransferType: {cellConfig.TransferType}");
                }
            }
        }

        #endregion // State Handlers

        #region Logging

        private void LogGameStart(bool fromResume)
        {
            using (var e = m_Log.NewEvent("game_start")) {
                e.Param("from_resume", fromResume);
            }
        }

        private void LogClickNewGame()
        {
            m_Log.NewEvent("click_new_game");
        }

        private void LogClickResumeGame()
        {
            m_Log.NewEvent("click_resume_game");
            SubmitGameState();
        }

        # region Overarching

        private void LogShipMenuDisplayed()
        {
            using (m_Log.NewEvent("ship_menu_displayed")) { }
        }

        private void LogAcceptContract(string contractId)
        /*
         "accept_contract": {
             "description": "When the player accepts a new contract.",
             "event_data": {
                "contract_id" : {
                   "type" : "str",
                   "description" : "The ID for the specific contract."
                },
                "contract_levels" : {
                   "type" : "Dict",
                   "description" : "JSON structure indicating the number of levels of each minigame, specified by the contract."
                }
             }
          }
         */
        {
            m_CurrentContract = contractId;
            if (!m_ContractLevels.ContainsKey(contractId))
            {
                m_ContractLevels[contractId] = new Dictionary<Minigame, int>();
            }
            SubmitGameState();

            m_JsonBuilder.Begin();
            foreach (var kvp in m_ContractLevels)
            {
                m_JsonBuilder.BeginObject(kvp.Key); // key: minigame
                foreach (var minigameKvp in kvp.Value)
                {
                    m_JsonBuilder.Field(minigameKvp.Key.ToString(), minigameKvp.Value); // value: number of levels
                }
                m_JsonBuilder.EndObject();
            }
            string levelsJson = m_JsonBuilder.End().ToString();

            using (var e = m_Log.NewEvent("accept_contract"))
            {
                e.Param("contract_id", contractId);
                e.Param("contract_levels", levelsJson);
            }
        }

        private void LogSelectContract(string contractId)
        {
            m_CurrentContract = contractId;
        }

        private void LogOpenContractView()
        {
            using (m_Log.NewEvent("open_contract_view")) { }
        }

        private void LogStartChangeContract(string contractId)
        {
            using (var e = m_Log.NewEvent("start_change_contract"))
            {
                e.Param("contract_id", contractId);
            }
        }
        private void LogConfirmSelectContract(string contractId)
        {
            m_CurrentContract = contractId;
            SubmitGameState();

            using (var e = m_Log.NewEvent("confirm_select_contract"))
            {
                e.Param("contract_id", contractId);
            }
        }

        private void LogCancelSelectContract(string contractId)
        {
            using (var e = m_Log.NewEvent("cancel_select_contract"))
            {
                e.Param("contract_id", contractId);
            }
        }

        private void HandleMinigameSelect(MinigameId zoneIndex)
        {
            switch(zoneIndex)
            {
                case MinigameId.Supply:
                    LogSelectSupplyChain();
                    break;
                case MinigameId.Design:
                    Debug.Log("Design selected");
                    LogSelectDesign();
                    break;
                case MinigameId.Fabrication:
                    LogSelectFabrication();
                    break;
                case MinigameId.Research:
                    LogSelectResearch();
                    break;
                default:
                    Log.Msg("[Logging] Unrecognized minigame zone index: {0}", zoneIndex);
                    break;
            }
        }

        // Ensure the indices, which is different from minigame zone select
        public void HandleMinigameStart(MinigameId zoneIndex)
        {
            switch(zoneIndex)
            {
                case MinigameId.Research:
                    LogStartResearch();
                    break;
                case MinigameId.Design:
                    Debug.Log("Design started");
                    LogStartDesign();
                    break;
                case MinigameId.Supply:
                    LogStartSupplyChain();
                    break;
                case MinigameId.Fabrication:
                    LogStartFabrication();
                    break;
                default:
                    Log.Msg("[Logging] Unrecognized minigame zone index: {0}", zoneIndex);
                    break;
            }
        }
        public void HandleMinigameExit()
        {
            switch(m_CurrentMinigame)
            {
                case Minigame.RESEARCH:
                    break;
                case Minigame.DESIGN:
                    LogExitDesign();
                    break;
                case Minigame.SUPPLY_CHAIN:
                    break;
                case Minigame.FABRICATION:
                    break;
                default:
                    Log.Msg("[Logging] Unrecognized minigame on exit: {0}", m_CurrentMinigame);
                    break;
            }
        }

        #endregion // Overarching

        #region Research
        private void LogSelectResearch()
        {
            using (m_Log.NewEvent("select_research")) { }
        }

        private void LogStartResearch()
        {
            m_CurrentMinigame = Minigame.RESEARCH;
            SubmitGameState();

            using (m_Log.NewEvent("start_research")) { }
        }

        #endregion // Research

        #region Design

        private void LogSelectDesign()
        {
            using (m_Log.NewEvent("select_design")) { }
        }

        private void LogStartDesign()
        {
            m_CurrentMinigame = Minigame.DESIGN;
            SubmitGameState();
            using (m_Log.NewEvent("start_design")) { }
        }

        private void LogDesignLevelBegin(GridStackConfig config)
        /*
         "design_level_begin": {
             "description": "When the player begins a new level in teh design minigame.",
             "event_data": {
                "initial_board_state" : {
                   "type" : "DesignGrid",
                   "description" : "The initial state of the design grid, when the player began the level."
                },
                "inputs" : {
                   "type" : "TBD",
                   "description" : "The inputs. Not 100% sure what this was. Possibly the coordinates of the input points?"
                },
                "outputs" : {
                   "type" : "TBD",
                   "description" : "The outputs. Not 100% sure what this was. Possibly the coordinates of the output point?"
                }
             }
          }
         */
        {

            // need clarification: design level is initialized when a contract is selected, but design_grid should be null when not in design minigame
            Debug.Log("Dispatch design level begin");

            List<(int, int)> inputs = new List<(int, int)>();
            List<(int, int)> outputs = new List<(int, int)>();

            foreach (GridCellConfig cell in config.Cells)
            {
                if (cell.CellType == CellType.Input)
                {
                    inputs.Add((cell.RowIndex, cell.ColumnIndex));
                }
                else if (cell.CellType == CellType.Output)
                {
                    outputs.Add((cell.RowIndex, cell.ColumnIndex));
                }
                else if (cell.CellType != CellType.NONE || cell.TransferType != TransferType.NONE)
                {
                    Vector2Int cellPos = new Vector2Int(cell.RowIndex, cell.ColumnIndex);
                    UpdateDesignGridState(cellPos, cell.CellType.ToString());
                    UpdateDesignGridState(cellPos, cell.TransferType.ToString());
                }
            }

            m_JsonBuilder.Clear();
            string gridJson = null;

            m_JsonBuilder.Begin();
            if (m_DesignGrid.HasValue)
            {
                m_JsonBuilder.BeginArray();
                m_DesignGrid.Value.Grid.ForEach(row => row.ForEach(cell =>
                {
                    m_JsonBuilder.BeginArray();
                    foreach (var toolId in cell)
                    {
                        m_JsonBuilder.Item(toolId.ToString());
                    }
                    m_JsonBuilder.EndArray();
                }));
                m_JsonBuilder.EndArray();
                gridJson = m_JsonBuilder.End().ToString();
            }

            using (var e = m_Log.NewEvent("design_level_begin"))
            {
                e.Param("initial_board_state", gridJson);
                e.Param("inputs", string.Join(";", inputs.Select(coord => $"({coord.Item1},{coord.Item2})")));
                e.Param("outputs", string.Join(";", outputs.Select(coord => $"({coord.Item1},{coord.Item2})")));
            }

        }

        private void LogSelectTool(ToolType toolType)
        {
            var toolId = ToolID.METAL; // by default
            switch(toolType)
            {
                case ToolType.None:
                    break;
                case ToolType.DrawMetal:
                    break;
                case ToolType.DrawPNodes:
                    toolId = ToolID.PTYPE;
                    break;
                case ToolType.DrawNNodes:
                    toolId = ToolID.NTYPE;
                    break;
                case ToolType.DrawVia:
                    toolId = ToolID.CONTACT;
                    break;
                case ToolType.DrawGate:
                    toolId = ToolID.GATE;
                    break;
                case ToolType.Erase:
                    break;
                default:
                    throw new ArgumentException($"Unrecognized ToolbarButtonKind: {toolType}");
            }

            using (var e = m_Log.NewEvent("select_tool"))
            {
                e.Param("tool_id", toolId.ToString());
            }
        }

        private void LogFillGrid(GridCoord coordinate, string tool)
        {
            var toolId = ToolID.METAL; // by default

            switch(tool)
            {
                case "Erase":
                    break;
                case "Metal":
                    toolId = ToolID.METAL;
                    break;
                case "PNodes":
                    toolId = ToolID.PTYPE;
                    tool = "PTransistor";
                    break;
                case "NNodes":
                    toolId = ToolID.NTYPE;
                    tool = "NTransistor";
                    break;
                case "Via":
                    toolId = ToolID.CONTACT;
                    break;
                case "Gate":
                    toolId = ToolID.GATE;
                    break;
                default:
                    throw new ArgumentException($"Unrecognized tool: {tool}");
            }

            UpdateDesignGridState(new Vector2Int(coordinate.Row, coordinate.Col), tool);

            using (var e = m_Log.NewEvent("fill_grid"))
            {
                e.Param("tool_id", toolId.ToString());
                e.Param("x", coordinate.Row);
                e.Param("y", coordinate.Col);
                e.Param("layer", coordinate.Layer.ToString());
            }
        }

        private void LogStartTest(List<GridCoord> inputs, List<GridCoord> outputs, int rowIndex)
        {
            // TODO
            using (var e = m_Log.NewEvent("start_test"))
            {
                e.Param("inputs", string.Join(";", inputs.Select(coord => $"({coord.Row},{coord.Col})")));
                e.Param("outputs", string.Join(";", outputs.Select(coord => $"({coord.Row},{coord.Col})")));
                e.Param("row_index", rowIndex);
            }
        }

        private void LogTestRowResolved(string resultStr, int rowIndex)
        {
            if (resultStr == "success") { LogTestSucceeded(rowIndex); }
            else { LogTestFailed(rowIndex); }
        }

        private void LogTestSucceeded(int rowIndex)
        {
            using (var e = m_Log.NewEvent("submission_succeeded"))
            {
                e.Param("row_number", rowIndex);
            }
        }

        private void LogTestFailed(int rowIndex)
        {
            using (var e = m_Log.NewEvent("submission_failed"))
            {
                e.Param("row_number", rowIndex); // need to confirm: are the rows of test suite always in the same order?
            }
        }

        private void LogDesignComplete()
        {
            m_Log.NewEvent("design_complete");
        }

        private void LogExitDesign()
        {
            m_CurrentMinigame = null;
            //m_DesignGrid = null;
            SubmitGameState();

            using (m_Log.NewEvent("exit_design")) { }
        }

        #endregion // Design

        #region Supply Chain

        private void LogSelectSupplyChain()
        {
            using (m_Log.NewEvent("select_supply_chain")) { }
        }

        private void LogStartSupplyChain()
        {
            m_CurrentMinigame = Minigame.SUPPLY_CHAIN;
            SubmitGameState();

            using (m_Log.NewEvent("start_supply_chain")) { }
        }

        #endregion // Supply Chain

 #region Fabrication

        private void LogSelectFabrication()
        {
            using (m_Log.NewEvent("select_fabrication")) { }
        }
        
        private void LogStartFabrication()
        {
            m_CurrentMinigame = Minigame.FABRICATION;
            SubmitGameState();

            using (m_Log.NewEvent("start_fabrication")) { }
        }

        private void LogGenerateWafer()
        {
            using (m_Log.NewEvent("generate_wafer")) { }
        }

        private void LogTimerStart()
        {
            using (m_Log.NewEvent("timer_start")) { } ;
        }

        private void LogInstructionUpdated(string nextStation, bool isHidden)
        /*
         "instruction_updated": {
            "description": "When the instruction shown to the player is updated.",
            "event_data": {
                "next_station" : {
                   "type" : "str",
                   "description" : "The next station the player needs to complete, as indicated by the instruction."
                },
                "is_hidden" : {
                   "type" : "bool",
                   "description" : "Whether the instruction is currently hidden from the player or not."
                }
            }
        }
        */
        {
            using (var e = m_Log.NewEvent("instruction_updated"))
            {
                e.Param("next_station", nextStation);
                e.Param("is_hidden", false); // TODO: set default to false for now
            }
        }

        private void LogActivateStation(string stationId)
        {
            using (var e = m_Log.NewEvent("activate_station"))
            {
                e.Param("station_id", stationId);
            }
        }

        private void LogInvalidActivation((string, string) stationIds)
        /*
         "invalid_activation": {
             "description": "When the player attempted to activate the wrong station.",
             "event_data": {
                "station_id" : {
                   "type" : "str",
                   "description" : "The ID of the station the player activated."
                },
                "next_station" : {
                   "type" : "str",
                   "description" : "The actual next station the player was supposed to activate."
                }
             }
          }
         */
        {
            using (var e = m_Log.NewEvent("invalid_activation"))
            {
                e.Param("station_id", stationIds.Item1);
                e.Param("next_station", stationIds.Item2);
            }
        }

        private void LogStationComplete(string stationId, float accuracy, bool isAutomated)
        {
            using (var e = m_Log.NewEvent("station_complete"))
            {
                e.Param("station_id", stationId);
                e.Param("accuracy", accuracy);
                e.Param("is_automated", isAutomated); // TODO: set default to false for now
            }
        }

        private void LogFabricationComplete()
        {
            m_CurrentMinigame = null;
            SubmitGameState();

            using (m_Log.NewEvent("fabrication_complete")) { } ;
        }

        private void LogUseAutomation(string stationId)
        {
            using (var e = m_Log.NewEvent("use_automation"))
            {
                e.Param("station_id", stationId);
            }
        }

        private void LogFabricationSuccess(float accuracy, float time, int productionCycles)
        {
            using (var e = m_Log.NewEvent("fabrication_success"))
            {
                e.Param("accuracy", accuracy);
                e.Param("time", time);
                e.Param("production_cycles", productionCycles);
            }
        }

        #endregion // Fabrication

        private void LogLevelMenuDisplayed()
        {
            using (m_Log.NewEvent("level_menu_displayed")) { } ;
        }

        private void LogSelectLevel(string levelId)
        {
            using (var e = m_Log.NewEvent("select_level"))
            {
                e.Param("level_id", levelId);
            }
        }

        
        #endregion // Logging
    }
}