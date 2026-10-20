using FieldDay.Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design
{
    [Flags]
    public enum ToolTypeFlags
    {
        NNODE = 1 << 0,
        PNODE = 1 << 1,
        METAL = 1 << 2,
        VIA = 1 << 3,
        GATE = 1 << 4,
        ERASER = 1 << 5,
        CLEAR = 1 << 6, // clear all
    }

    [Flags]
    public enum InputOutputNodeTypeFlags
    {
        VMINUS = 1 << 0,
        VPLUS = 1 << 1,
        IN = 1 << 2,
        A = 1 << 3,
        B = 1 << 4,
        C = 1 << 5,
        OUT = 1 << 6,
        OUTX = 1 << 7,
        OUTY = 1 << 8,
        OUTZ = 1 << 9,
    }

    [CreateAssetMenu(menuName = "SpaceFab/Design/Level Data")]
    public class LevelData : NamedAsset
    {
        [SerializeField] private string m_title;
        [SerializeField, TextArea] private string m_description;
        [SerializeField] private ToolTypeFlags m_allowedTools;
        [SerializeField] private GridStackConfig m_gridConfig;
        [SerializeField] private TestSuiteData m_testSuite;

        public string GetTitle() { return m_title; }
        public string GetDescription() { return m_description; }
        public ToolTypeFlags GetAllowedTools() { return m_allowedTools; }
        public GridStackConfig GetGridConfig() { return m_gridConfig; }
        public TestSuiteData GetTestSuite() { return m_testSuite; }
    }

    public class LevelDataCopy
    {
        [SerializeField] private string m_title;
        [SerializeField] private string m_description;
        [SerializeField] private ToolTypeFlags m_allowedTools;
        [SerializeField] private GridStackConfig m_gridConfig;
        [SerializeField] private TestSuiteData m_testSuite;

        public string GetTitle() { return m_title; }
        public string GetDescription() { return m_description; }
        public ToolTypeFlags GetAllowedTools() { return m_allowedTools; }
        public GridStackConfig GetGridConfig() { return m_gridConfig; }
        public TestSuiteData GetTestSuite() { return m_testSuite; }


        public void LoadData(LevelData data)
        {
            m_title = data.GetTitle();
            m_description = data.GetDescription();
            m_allowedTools = data.GetAllowedTools();
            m_gridConfig = data.GetGridConfig();
            m_testSuite = data.GetTestSuite();
        }
    }
}