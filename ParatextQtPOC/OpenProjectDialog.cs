using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Paratext.Data;
using QtCore;
using QtCore.Qt;
using QtWidgets;

namespace ParatextQtPOC
{
    internal class OpenProjectDialog : QDialog
    {
        private readonly QTableWidget projectList;
        private readonly QPushButton okButton;
        private readonly List<ScrText> scrTexts;

        public OpenProjectDialog()
        {
            WindowModality = WindowModality.WindowModal;

            QGridLayout layout = new QGridLayout(this);
            layout.SetColumnStretch(0, 100);
            layout.SetRowStretch(0, 100);

            scrTexts = ScrTextCollection.ScrTexts(IncludeProjects.ScriptureOnly).ToList();

            projectList = new QTableWidget(scrTexts.Count, 3, this);
            projectList.ItemSelectionChanged += ProjectList_ItemSelectionChanged;
            projectList.selectionBehavior = QAbstractItemView.SelectionBehavior.SelectRows;
            projectList.selectionMode = QAbstractItemView.SelectionMode.SingleSelection;
            projectList.SetHorizontalHeaderItem(0, new QTableWidgetItem("Short name"));
            projectList.SetHorizontalHeaderItem(1, new QTableWidgetItem("Long name"));
            projectList.SetHorizontalHeaderItem(2, new QTableWidgetItem("Language"));
            projectList.MinimumSize = new QSize(500, 300);

            int row = 0;
            foreach (ScrText scrText in scrTexts)
            {
                projectList.SetItem(row, 0, new QTableWidgetItem(scrText.Name));
                projectList.SetItem(row, 1, new QTableWidgetItem(scrText.Settings.FullName));
                projectList.SetItem(row, 2, new QTableWidgetItem(scrText.LanguageName));
                row++;
            }
            layout.AddWidget(projectList, 0, 0, AlignmentFlag.AlignJustify);

            okButton = new QPushButton("OK");
            okButton.Enabled = false;
            okButton.Clicked += OkButton_Clicked;
            layout.AddWidget(okButton, 1, 0, AlignmentFlag.AlignRight);
            
            QPushButton cancelButton = new QPushButton("Cancel");
            cancelButton.Clicked += CancelButton_Clicked;
            layout.AddWidget(cancelButton, 1, 1, AlignmentFlag.AlignLeft);

            Layout = layout;
            Resize(600, 300);
        }

        public ScrText SelectedProject { get; private set; }

        private void ProjectList_ItemSelectionChanged()
        {
            okButton.Enabled = projectList.CurrentRow >= 0;
        }

        private void CancelButton_Clicked(bool obj)
        {
            Result = (int)DialogCode.Rejected;
            Close();
        }

        private void OkButton_Clicked(bool obj)
        {
            Debug.Assert(projectList.CurrentRow >= 0 && projectList.CurrentRow < scrTexts.Count);
            Result = (int)DialogCode.Accepted;
            SelectedProject = scrTexts[projectList.CurrentRow];
            Close();
        }
    }
}
