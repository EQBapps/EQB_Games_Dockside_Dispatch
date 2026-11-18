using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Build-time switches. Test tools (Go to level, Skip level, Sky preview) are compiled in only for the editor and
    /// for builds made with Dockside > Build Android APK (test tools), which add the DOCKSIDE_TEST_TOOLS define.
    /// Store builds never contain them.
    /// </summary>
    public static class BuildFlags
    {
#if DOCKSIDE_TEST_TOOLS
        private const bool TestToolsDefine = true;
#else
        private const bool TestToolsDefine = false;
#endif
        public static bool TestTools => Application.isEditor || TestToolsDefine;
    }
}
