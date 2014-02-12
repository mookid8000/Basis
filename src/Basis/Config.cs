namespace Basis
{
    internal class Config
    {
        public static BuildConfig CurrentBuildConfig
        {
            get
            {
#if DEBUG
                return BuildConfig.Debug;
#else
                return BuildConfig.Release;
#endif
            }
        }

        public enum BuildConfig
        {
            Debug,
            Release
        }
    }
}
