using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SVNMailer
{
	public class SVNMailer
	{
		private static string m_emailServer;
		private static string m_sendEmailAddress;
		private static string m_svnLookLocation;
		private static string m_viewRevisionURL;
		private static string m_viewDiffURL;
		private static string m_viewViewFileURL;
		private static string m_viewDirectoryURL;
		private static string m_bugURL;
		private static string m_activeDirectoryServer;

		private static string m_author;
		private static string m_log;
		private static string m_changedDate;

		static void Main(string[] args)
		{
			//args[0] - Repository location
			//args[1] - Revision number
			string repository = args[0].Split('=')[1];
			string revision = args[1].Split('=')[1];

			LoadConfiguration();
			ParseInfo(repository, revision);

			StringBuilder emailToSend = new StringBuilder();

			emailToSend.Append("<html><body>");

			emailToSend.Append(BuildHeader(repository, revision, m_author));
			emailToSend.Append(BuildChangeList(repository, revision));
			emailToSend.Append(BuildFooter());

			emailToSend.Append("</body></html>");

			if (!String.IsNullOrEmpty(emailToSend.ToString()) && !String.IsNullOrEmpty(m_author))
			{
				Utilities.SendMail(m_author, emailToSend.ToString(), revision, m_sendEmailAddress, m_emailServer, m_activeDirectoryServer);
			}
		}

		private static void LoadConfiguration()
		{
			m_emailServer = ConfigurationManager.AppSettings["EmailServer"];
			m_sendEmailAddress = ConfigurationManager.AppSettings["SendEmailAddress"];
			m_svnLookLocation = ConfigurationManager.AppSettings["SVNLookLocation"];
			m_viewRevisionURL = ConfigurationManager.AppSettings["ViewRevisionURL"];
			m_viewDiffURL = ConfigurationManager.AppSettings["ViewDiffURL"];
			m_viewViewFileURL = ConfigurationManager.AppSettings["ViewFileURL"];
			m_viewDirectoryURL = ConfigurationManager.AppSettings["ViewDirectoryURL"];
			m_bugURL = ConfigurationManager.AppSettings["BugURL"];
			m_activeDirectoryServer = ConfigurationManager.AppSettings["ActiveDirectoryServer"];
		}

		private static string BuildHeader(string repository, string revision, string author)
		{
			//Attempt to locate the name for this user in active directory
			string userName = String.IsNullOrEmpty(m_activeDirectoryServer)
			                  	? author
			                  	: String.Format("{0} ({1})", author, Utilities.GetUserName(m_activeDirectoryServer, author));

			StringBuilder headerBuilder = new StringBuilder();

			headerBuilder.Append("<style>");
			headerBuilder.Append("HR { WIDTH:100%; COLOR:Gray; Text-Align:Center } ");
			headerBuilder.Append(".header TD { FONT-SIZE: 10pt; FONT-FAMILY: Arial; } ");
			headerBuilder.Append(".header TH { FONT-SIZE: 10pt; FONT-FAMILY: Arial;Text-Align:Left; }");
			headerBuilder.Append(".changeList TD { BACKGROUND-COLOR:#EEEEEE; FONT-SIZE: 10pt; FONT-FAMILY: Arial; } ");
			headerBuilder.Append(".changeList TH { BACKGROUND-COLOR:#FFFFFF; FONT-SIZE: 10pt; FONT-FAMILY: Arial; COLOR: Green; Text-Align:Left; }");
			headerBuilder.Append(".message { FONT-SIZE: 10pt; FONT-FAMILY: Arial; }");
			headerBuilder.Append("</style>");
			headerBuilder.AppendFormat("<table class='header'>");
			headerBuilder.AppendFormat("<tr><th>SVN&nbsp;Server:</th><td style='width:100%'>{0}</td></tr>", Environment.MachineName);
			headerBuilder.AppendFormat("<tr><th>Repository:</th><td>{0}</td></tr>", Path.GetFileName(repository));
			headerBuilder.AppendFormat("<tr><th>Date&nbsp;of change:</th><td>{0}</td>", m_changedDate);
			headerBuilder.AppendFormat("<tr><th>Changes&nbsp;by:</th><td>{0}</td></tr>", userName);
			headerBuilder.AppendFormat("<tr><th>Log&nbsp;Message:</th><td>{0}</td></tr>", m_log);
			headerBuilder.AppendFormat("<tr><th>Revision&nbsp;URL:</th><td><a href={0}>Revision {1}</a></td></tr></table>", String.Format(m_viewRevisionURL, revision), revision);
			headerBuilder.Append(@"<hr size=2 noshade>");

			return headerBuilder.ToString();
		}

		private static string BuildChangeList(string repository, string revision)
		{
			Dictionary<DirectoryPathInfo, List<ChangedPathInfo>> changes = new Dictionary<DirectoryPathInfo, List<ChangedPathInfo>>(); 
			StringBuilder changeBuilder = new StringBuilder();

			ConstructChangedFilesList(changes, repository, revision);

			changeBuilder.Append("<table class='changeList'>");
			changeBuilder.Append("<tr><th style='width:100%'>Affected Files/Folders</th><th>Action</th><th>Branch</th><th>Diff</th></tr>");

			foreach (DirectoryPathInfo directory in changes.Keys ) {
				changeBuilder.Append(directory.DirectoryRow);

				foreach ( ChangedPathInfo filePath in changes[directory] ) {
					changeBuilder.Append(filePath.FileRow);
				}
			}
	
			changeBuilder.AppendFormat("</table>");
			changeBuilder.Append(@"<hr size=2 noshade>");

			return changeBuilder.ToString();
		}

		private static string BuildFooter()
		{
			StringBuilder footerBuilder = new StringBuilder();

			footerBuilder.Append("<span class='message'>This is an automated message sent by SVN on behalf of the user.<br>");
			footerBuilder.Append(@"Message prepared by the SVN Mailer.</span>");

			return footerBuilder.ToString();
		}

		private static void ParseInfo(string repository, string revision)
		{
			string originalLog = Utilities.RetrieveRevisionInfo(repository, revision, m_svnLookLocation);
			string[] splitLog = originalLog.Replace("\r", "").Split('\n');

			m_author = splitLog[0];
			m_changedDate = splitLog[1];

			//Add 2 characters for each \r\n combination - hence, '6' characters
			m_log = originalLog.Substring(splitLog[0].Length + splitLog[1].Length + splitLog[2].Length + 6);

			//Add a bug link to any bug links in the revision log
			if (m_bugURL != String.Empty) {
				Regex bugMatch = new Regex(@"\[BUG(\s\d+)\]", RegexOptions.IgnoreCase);

				m_log = bugMatch.Replace(m_log, new MatchEvaluator(AddBugLink));
			}
		}

		private static string AddBugLink(Match match)
		{
			//The second group matches the digits inside the bug string
			return String.Format(m_bugURL, match.Groups[1].ToString().Trim(), match.Value);
		}

		private static void ConstructChangedFilesList(Dictionary<DirectoryPathInfo, List<ChangedPathInfo>> changes, string repository, string revision)
		{
			//Get the list of files
			string[] changedFiles = Utilities.RetrieveChangedFiles(repository, revision, m_svnLookLocation).Replace("\r", "").Split('\n');

			//For each file build up a ChangedPathInfo object, determining its file name, action, diff link (if necessary), and branch
			foreach (string changedFile in changedFiles) {
				if (changedFile != String.Empty) {
					//Note that we need to skip the first two chars of the file path, as that contains the 'Added,' 'Updated,' or 'Removed' flag
					string action = changedFile.Substring(0, 2).TrimEnd();
					string filePath = changedFile.Substring(4);

					//Don't bother continuing if the path doesn't have a file name, it's a directory (and we don't care about showing the
					//addition of those in the diff email
					if (Path.GetFileName(filePath) != String.Empty) {
						ChangedPathInfo pathInfo = new ChangedPathInfo(action,
						                                   filePath,
														   m_viewDiffURL,
														   m_viewViewFileURL,
						                                   revision);

						//Remove the file name from the path
						string fullPath = filePath.Substring(0, filePath.IndexOf(Path.GetFileName(filePath)) - 1);

						//Determine if the folder the file is in is inside the hash table
						DirectoryPathInfo directoryPathInfo = new DirectoryPathInfo(fullPath, pathInfo.Branch, ChangedState.U.ToString(), m_viewDirectoryURL);

						//If so, add the new object to that path in the hash table
						if (changes.ContainsKey(directoryPathInfo)) {
							changes[directoryPathInfo].Add(pathInfo);
						} else {
							//If not, add that directory path to the hash table as the key, and add it to the list of files in that path
							changes.Add(directoryPathInfo, new List<ChangedPathInfo>{pathInfo});
						}
					} else {
						//If there is no file name, we have a directory path by itself - this means that the directory
						//was either added, deleted, or had properties changed, otherwise it wouldn't show up in the changed list.
						string branch = Utilities.DetermineBranch(filePath);

						//Determine if the folder the file is in is inside the hash table
						DirectoryPathInfo directoryPathInfo = new DirectoryPathInfo(filePath, branch, action, m_viewDirectoryURL);

						//If not, add that directory path to the hash table as the key, and add a blank list for any files that might later fall in this directory
						if (!changes.ContainsKey(directoryPathInfo)) {
							changes.Add(directoryPathInfo, new List<ChangedPathInfo>());
						}
					}
				}
			}
		}
	}
}
