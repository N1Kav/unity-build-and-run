using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class BuildAndRun : EditorWindow
{
    private static BuildAndRun _window;
    private static string _androidSdkRoot;
    private static string _packageName;
    private static Dictionary<string, bool> _devicesToggles;

    private static bool _selectedAll = true;
    private static Vector2 _scrollPosition = Vector2.zero;
    private static readonly Vector2 WindowSize = new Vector2( 190f, 220f );

    private const string MAIN_ACTIVITY_STRING = "com.prime31.UnityPlayerProxyActivity";
    private const string APK_NAME = "game-and";

    private static string _searchWord = "";
    private static List<string> _searchDevices = new List<string>();

    [MenuItem( "Unik/Build And Run", false, 1 )]
    public static void Init()
    {
        if ( _window == null )
            _window = (BuildAndRun)GetWindow( typeof ( BuildAndRun ), false, "Build And Run" );
        _window.minSize = WindowSize;
    }

    [PostProcessBuild]
    public static void Install( BuildTarget target, string pathToBuiltProject )
    {
        if ( EditorPrefs.GetInt( "PF_POST_PROCESS" ) != 1 )
            return;

        if ( !File.Exists( pathToBuiltProject ) )
        {
            Debug.Log( pathToBuiltProject + " not exists!" );
            EditorPrefs.SetInt( "PF_POST_PROCESS", 0 );
            return;
        }

        _androidSdkRoot = EditorPrefs.GetString( "AndroidSdkRoot" );
        LoadDevices();
        _packageName = PlayerSettings.bundleIdentifier;

        foreach ( var pair in _devicesToggles )
        {
            var deviceName = pair.Key;
            if ( !pair.Value )
                continue;
            var isDone = false;
            var isFail = false;
            var process =
                System.Diagnostics.Process.Start(
                    GetProcessInfo( "-s " + deviceName + " install -r " + pathToBuiltProject ) );
            var thread = new Thread( () =>
                                     {
                                         while ( process != null && !process.StandardOutput.EndOfStream )
                                         {
                                             var line = process.StandardOutput.ReadLine();
                                             if ( line != null && line.Contains( "Failure" ) )
                                             {
                                                 isFail = true;
                                                 Debug.LogError( "Install: " + line );
                                             }
                                         }
                                         isDone = true;
                                     } );
            thread.Start();

            while ( !isDone )
            {
            }

            if ( !isFail )
                Run( deviceName );
        }

        EditorPrefs.SetInt( "PF_POST_PROCESS", 0 );
    }

    [DidReloadScripts]
    public static void DidReloadScripts()
    {
        if ( EditorPrefs.GetInt( "PF_POST_PROCESS" ) != 1 )
            return;

        _androidSdkRoot = EditorPrefs.GetString( "AndroidSdkRoot" );
        LoadDevices();
        _packageName = PlayerSettings.bundleIdentifier;
    }

    void Reset()
    {
        GetADBDevices();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
        if ( GUILayout.Button( "Get devices", EditorStyles.toolbarButton, GUILayout.Width( 70 ),
                               GUILayout.ExpandWidth( true ) ) )
        {
            GetADBDevices();
        }
        GUILayout.Space( 2 );
        if ( _searchWord !=
             (_searchWord =
                 EditorGUILayout.TextField( _searchWord, GUI.skin.FindStyle( "ToolbarSeachTextField" ),
                                            GUILayout.ExpandWidth( true ) )) )
        {
            Search();
        }
        if ( GUILayout.Button( "", GUI.skin.FindStyle( "ToolbarSeachCancelButton" ) ) )
        {
            _searchWord = "";
            GUI.FocusControl( "" );
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if ( _selectedAll != (_selectedAll = EditorGUILayout.Toggle( "Devices:", _selectedAll )) )
            SelectedAll( _selectedAll );
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Separator();
        _scrollPosition = EditorGUILayout.BeginScrollView( _scrollPosition, false, false, GUILayout.ExpandWidth( true ),
                                                           GUILayout.Height( 120 ) );
        foreach ( var deviceName in _searchDevices )
        {
            if ( _devicesToggles[deviceName] !=
                 (_devicesToggles[deviceName] = EditorGUILayout.Toggle( deviceName, _devicesToggles[deviceName] )) )
            {
                CheckSelectAll();
                SaveDevices();
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Separator();

        if ( GUILayout.Button( "Build and run" ) )
            Build();
        if ( GUILayout.Button( "Install apk" ) )
        {
            EditorPrefs.SetInt( "PF_POST_PROCESS", 1 );
            Install( BuildTarget.Android, "Assets/Builds/" + APK_NAME + ".apk" );
        }
    }

    private void Build()
    {
        EditorPrefs.SetInt( "PF_POST_PROCESS", 1 );

        var scenes = new List<string>();
        foreach ( var scene in EditorBuildSettings.scenes )
        {
            if ( scene.enabled )
                scenes.Add( scene.path );
        }

        if ( !Directory.Exists( "Assets/Builds/" ) )
            Directory.CreateDirectory( "Assets/Builds/" );
        const string path = "Assets/Builds/" + APK_NAME + ".apk";
        BuildPipeline.BuildPlayer( scenes.ToArray(), path, BuildTarget.Android, BuildOptions.Development );
    }

    private static void Run( string device )
    {
        var isDone = false;
        var process =
            System.Diagnostics.Process.Start(
                GetProcessInfo( "-s " + device + " shell am start -n" + _packageName + "/" + MAIN_ACTIVITY_STRING ) );
        var thread = new Thread( () =>
                                 {
                                     while ( process != null && !process.StandardOutput.EndOfStream )
                                     {
                                         /*string line = */
                                         process.StandardOutput.ReadLine();
                                         //Debug.Log( "Run: " + line );
                                     }
                                     isDone = true;
                                 } );
        thread.Start();

        while ( !isDone )
        {
        }
    }

    private static System.Diagnostics.ProcessStartInfo GetProcessInfo( string args )
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = _androidSdkRoot + "/platform-tools/adb.exe",
            Arguments = args,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        //Debug.Log( startInfo.FileName + " " + args );
        return startInfo;
    }

    private static void GetADBDevices()
    {
        _androidSdkRoot = EditorPrefs.GetString( "AndroidSdkRoot" );
        _packageName = PlayerSettings.bundleIdentifier;
        var devicesTogglesTemp = new Dictionary<string, bool>();

        var complete = false;
        var process = System.Diagnostics.Process.Start( GetProcessInfo( "devices" ) );
        var thread = new Thread( () =>
                                 {
                                     while ( process != null && !process.StandardOutput.EndOfStream )
                                     {
                                         var line = process.StandardOutput.ReadLine();
                                         if ( line == null )
                                             continue;
                                         var split = line.Split( '\t' );
                                         if ( split.Length > 1 && split[1] == "device" )
                                             devicesTogglesTemp.Add( split[0], true );
                                     }
                                     complete = true;
                                 } );
        thread.Start();

        while ( !complete )
        {
        }

        _devicesToggles = new Dictionary<string, bool>( devicesTogglesTemp );
        SaveDevices();
        _selectedAll = true;
        Search();
    }

    private static void SaveDevices()
    {
        var devices = "";
        foreach ( var pair in _devicesToggles )
            devices += pair.Key + "/" + pair.Value + ",";

        devices = devices.Trim( ',' );

        EditorPrefs.SetString( "ADBDevices", devices );
    }

    private static void LoadDevices()
    {
        var devices = EditorPrefs.GetString( "ADBDevices" ).Split( ',' );
        var devicesTogglesTemp = new Dictionary<string, bool>();
        foreach ( var device in devices )
        {
            var split = device.Split( '/' );
            devicesTogglesTemp.Add( split[0], split[1] == bool.TrueString );
        }
        _devicesToggles = new Dictionary<string, bool>( devicesTogglesTemp );
    }

    private void SelectedAll( bool selectedAll )
    {
        if ( _devicesToggles == null )
            return;
        var devices = new List<string>( _devicesToggles.Keys );
        foreach ( var deviceName in devices )
            _devicesToggles[deviceName] = selectedAll;
    }

    private void CheckSelectAll()
    {
        foreach ( var pair in _devicesToggles )
        {
            if ( pair.Value )
                continue;
            _selectedAll = false;
            return;
        }
        _selectedAll = true;
    }

    private static void Search()
    {
        if ( string.IsNullOrEmpty( _searchWord.Trim() ) )
        {
            _searchDevices = new List<string>( _devicesToggles.Keys );
        }
        else
        {
            var devices = new List<string>( _devicesToggles.Keys );
            _searchDevices = new List<string>();
            foreach ( var device in devices.Where( device => device.ToLower().Contains( _searchWord.ToLower() ) ) )
                _searchDevices.Add( device );
        }
    }
}
