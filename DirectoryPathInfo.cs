using System;

namespace SVNMailer
{
	public class DirectoryPathInfo
	{
		private readonly string m_directoryName;
		private readonly string m_directoryPath = String.Empty;
		private readonly string m_branch;
		private readonly ChangedState m_action;

		public DirectoryPathInfo(string fullPath, string branch, string action, string diffViewerDirectory)
		{
			m_action = Utilities.GetActionEnum(action);
			m_branch = branch;
			m_directoryName = fullPath;

			//Determine the directory name
			//Remove anything below the branch name from the path
			if (fullPath.IndexOf(branch) > 0) {
				int branchLocation = fullPath.IndexOf(branch) + branch.Length + 1;

				if (branchLocation < fullPath.Length) {
					m_directoryName = fullPath.Substring(fullPath.IndexOf(branch) + branch.Length + 1);
					//Subtract off the trailing backslash if one exists
					if(m_directoryName.LastIndexOf(@"/") == m_directoryName.Length - 1) {
						m_directoryName = m_directoryName.Substring(0, m_directoryName.Length - 1);						
					}
				} else {
					m_directoryName = String.Empty;
				}
			}

			//Determine the link to the directory
			//If we've deleted the directory, we don't want to provide a link to that directory
			if (m_action == ChangedState.D) {
				m_directoryPath = m_directoryName == String.Empty
				                  	? m_branch
				                  	: m_directoryName;
			} else {
				m_directoryPath = String.Format("<a href='{0}'>{1}</a>",
												String.Format(diffViewerDirectory, fullPath),
				                                m_directoryName == String.Empty
				                                	? m_branch
				                                	: m_directoryName);
			}
		}

		public string DirectoryRow
		{
			get
			{
				return String.Format("<tr><td><b>{0}</b></td><td>{1}</td><td></td><td></td></tr>",
				                     m_directoryPath,
				                     //"Updated" is not a notable action for a directory, so we ignore it
				                     m_action == ChangedState.U ? String.Empty : Utilities.GetEnumDescription(m_action));
			}
		}

		//These overrides required for ContainsKey on the dictionary to work properly
		public override bool Equals(object otherObj)
		{
			DirectoryPathInfo that = (DirectoryPathInfo) otherObj;

			return that.m_directoryName == m_directoryName;
		}

		public override int GetHashCode()
		{
			return m_directoryName.GetHashCode();
		}
	}
}
