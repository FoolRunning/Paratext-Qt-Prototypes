using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Paratext.Base.ProjectFileAccess;
using Paratext.Data;
using Paratext.Data.Languages;
using Paratext.Data.Users;
using PtxUtils.Progress;
using SIL.WritingSystems;

namespace Paratext.Base
{
    /// <summary>
    /// Implementation of the ScrTextCollection for Paratext
    /// </summary>
    public class ParatextScrTextCollection : ScrTextCollection
    {
        // keep track of languages that weren't found in SLDR so we don't call over and over for the same bad code
        private static readonly HashSet<string> sldrLookupFailed = new HashSet<string>();

        private readonly Func<ScrText, UnsupportedReason> migrateProjectFunc;
        private readonly Func<string, ScrText> marbleResourceLookup;

        public ParatextScrTextCollection(Func<ScrText, UnsupportedReason> migrateProjectFunc, Func<string, ScrText> marbleResourceLookup)
        {
            this.migrateProjectFunc = migrateProjectFunc;
            this.marbleResourceLookup = marbleResourceLookup;
        }

        /// <summary>
        /// Initializes the ScrTextCollection (and related) for Paratext
        /// </summary>
        protected override void InitializeInternal(string settingsDir, bool allowMigration)
        {
            if (!ProgressUtils.OnMainUiThread)
                throw new InvalidOperationException("ScrTextCollectionUI.Initalize must be called on the UI thread.");

            base.InitializeInternal(settingsDir, allowMigration);
        }

        protected override ScrText CreateResourceProject(ProjectName name)
        {
            return new ResourceScrText(name, RegistrationInfo.DefaultUser, new ParatextZippedResourcePasswordProvider());
        }

        protected override ScrText CreateXmlResourceProject(ProjectName name)
        {
            return new XmlResourceScrText(name, RegistrationInfo.DefaultUser, new ParatextZippedResourcePasswordProvider());
        }

        protected override ScrText MarbleResourceLookup(string name)
        {
            return marbleResourceLookup?.Invoke(name);
        }

        protected override UnsupportedReason MigrateProjectIfNeeded(ScrText scrText)
        {
            return migrateProjectFunc?.Invoke(scrText) ?? UnsupportedReason.CannotUpgrade;
        }

        protected override void DeleteDirToRecycleBin(string dir)
        {
            Directory.Delete(dir, true);
        }

        protected override WritingSystemDefinition CreateWsDef(string languageId, bool allowSldr)
        {
            // only check SLDR if allowed for this call and all internet access is enabled - SLDR isn't set up to use proxy
            WritingSystemDefinition wsDef = null;
            if (allowSldr && InternetAccess.Status == InternetUse.Enabled && !sldrLookupFailed.Contains(languageId))
            {
                try
                {
                    var sldrFactory = new SldrWritingSystemFactory();
                    sldrFactory.Create(languageId, out wsDef);
                }
                catch (Exception e)
                {
                    // ignore any SLDR errors - there have been problems with entries on the server failing to parse.
                    // also the id being provided may not be valid
                    Trace.TraceWarning("Getting {0} from SLDR failed: {1}", languageId, e);
                    sldrLookupFailed.Add(languageId);
                }
            }
            return wsDef;
        }

        protected override string SelectSettingsFolder()
        {
            return null;
        }
    }
}
