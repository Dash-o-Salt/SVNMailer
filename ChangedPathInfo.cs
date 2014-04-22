using System;
using System.Diagnostics;
using System.IO;

namespace SVNMailer
{
	public class ChangedPathInfo
	{
		private readonly ChangedState m_action;
		private readonly string m_link;
		private readonly string m_branch;
		private readonly string m_fileLink;
		private readonly string m_fileName;

		public ChangedPathInfo(string action, string filePath, string diffLink, string viewFileLink, string revision)
		{
			m_action = Utilities.GetActionEnum(action);
			m_branch = Utilities.DetermineBranch(filePath);

			if (m_action == ChangedState.U || m_action == ChangedState._U || m_action == ChangedState.UU) {
				m_link = String.Format("<a href='{0}'>Diff&nbsp;Link</a>",
				                           String.Format(diffLink, filePath, revision, (Convert.ToInt32(revision) - 1), revision));
			} else if (m_action == ChangedState.A) {
				m_link = String.Format("<a href='{0}'>File&nbsp;Link</a>", String.Format(viewFileLink, filePath, revision));
			}

			m_fileName = Path.GetFileName(filePath);
			m_fileLink = String.Format("<a href='{0}'>{1}</a>", String.Format(viewFileLink, filePath, revision), m_fileName);
		}

		public string Branch
		{
			[DebuggerStepThrough]
			get { return m_branch; }
		}

		public string FileRow
		{
			get
			{
				return String.Format("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>",
				                     m_action == ChangedState.D ? m_fileName : m_fileLink,
				                     Utilities.GetEnumDescription(m_action),
				                     m_branch,
				                     m_link);
			}
		}
	}
}
