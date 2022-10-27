using Paratext.Data.ProjectFileAccess;
using PtxUtils;

namespace Paratext.Base.ProjectFileAccess
{
    public class ParatextZippedResourcePasswordProvider : IZippedResourcePasswordProvider
    {
        private const string passwordHash = "i8Ab5Lqk2Hod5Nzn7WUhd0ZUuu1CSx2VYjo5DFq4ZIxw6Qd5Sxh8Ph6SRt9Zxl7J";
        private string cachedValue;

        public string GetPassword()
        {
            if (cachedValue == null)
                cachedValue = StringUtils.DecryptStringFromBase64("W5b9xwbZZp7qF/M/3m4ElFSRS35DyMcw", passwordHash);
            return cachedValue;
        }
    }
}