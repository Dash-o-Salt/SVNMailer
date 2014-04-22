using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SVNMailer
{
	public enum ChangedState
	{
		[Description("Added")]
		A,
		[Description("Deleted")]
		D,
		[Description("Updated")]
		U,
		[Description("SVN&nbsp;Properties&nbsp;Updated")]
		_U,
		[Description("Content&nbsp;&amp;&nbsp;SVN&nbsp;Properties&nbsp;Updated")]
		UU
	}

	public class Utilities
	{
		private static HashSet<string> m_branchList = new HashSet<string>();
		private static HashSet<string> m_basePath = new HashSet<string>();

		public static string DetermineBranch(string changedFilePath)
		{
			//By default, no 'branch' will be shown in the email,
			//and the full path will be shown for all directories (if we don't figure out what the branch is here).
			string branch = String.Empty;
			m_basePath.Add(changedFilePath.Split('/')[0]);

			//Determine what branch the changes are in.
			//Note that we may not have a known branch if we're checking into an unknown folder.
			if (changedFilePath.IndexOf("SourceRoots/branches/") > -1 || changedFilePath.IndexOf("SourceRoots/shelves/") > -1)
			{
				//We determine the branch by starting at SourceRoots/branches/, then going to the next forward slash (/).
				//0 - U SourceRoots
				//1 - branches
				//2 - Branch name
				branch = changedFilePath.Split('/')[2];
			}
			else if (changedFilePath.IndexOf("SourceRoots/trunk/") > -1)
			{
				branch = "trunk";
			}

			//As this is a hash set, if we add a duplicate, it simply fails to add itself to the set
			m_branchList.Add(branch);

			return branch;
		}

		private static string GetBranchList()
		{
			StringBuilder branchListBuilder = new StringBuilder();

			foreach ( string branchName in m_branchList ) {
				branchListBuilder.AppendFormat("{0}; ", branchName);
			}

			//If we have contents, return a list of branch(es) the commit occurred in
			if (branchListBuilder.Length > 2) {
				branchListBuilder.Length = branchListBuilder.Length - 2;

				return String.Format("on branch {0}", branchListBuilder);
			}

			branchListBuilder.Length = 0;

			//If we have contents, return a list of folder(s) the commit occurred in
			foreach(string basePath in m_basePath) {
				branchListBuilder.AppendFormat("{0}; ", basePath);
			}

			if (branchListBuilder.Length > 2) {
				branchListBuilder.Length = branchListBuilder.Length - 2;

				return String.Format("in folder {0}", branchListBuilder);
			}

			return String.Empty;
		}

		public static void SendMail(string emailFrom, string emailContents, string revision, string emailAddress, string emailServer, string activeDirectoryServer)
		{
			string userName = GetUserName(activeDirectoryServer, emailFrom);

			MailMessage message = new MailMessage();
			message.To.Add(emailAddress);
			message.Subject = String.Format("SVN commit {0} (Revision {1})", GetBranchList(), revision);
			message.From = new MailAddress(String.Format("{0}@{1}", emailFrom, emailServer), userName);
			message.Body = emailContents;
			message.IsBodyHtml = true;
			SmtpClient smtp = new SmtpClient(emailServer);
			smtp.Send(message);
		}

		public static string GetUserName(string activeDirectoryServer, string userLogon)
		{
			try {
				DirectoryEntry entry = new DirectoryEntry(String.Format("LDAP://{0}", activeDirectoryServer));

				DirectorySearcher deSearch = new DirectorySearcher();

				deSearch.SearchRoot = entry;
				deSearch.Filter = String.Format("(&(objectClass=user) (mail={0}*))", userLogon);

				SearchResult result = deSearch.FindOne();

				if (result != null) {
					ResultPropertyCollection properties = result.Properties;

					return properties["name"][0].ToString();
				}

				return userLogon;
			} catch (Exception) {
				//Any error? We'll return the user's logon name.
				return userLogon;
			}
		}

		public static string RetrieveChangedFiles(string repo, string revision, string toolsLocation)
		{
			return RetrieveRevisionInformation(repo, revision, "changed", toolsLocation);
		}

		public static string RetrieveRevisionInfo(string repo, string revision, string toolsLocation)
		{
			return RetrieveRevisionInformation(repo, revision, "info", toolsLocation);
		}

		public static string RetrieveRevisionInformation(string repo, string revision, string infoCommand, string toolsLocation)
		{
			return ExecuteSVNLook(String.Format("{2} {0} -r {1}", repo, revision, infoCommand), toolsLocation);
		}

		private static string ExecuteSVNLook(string arguments, string toolsLocation)
		{
			ProcessStartInfo psi = new ProcessStartInfo(toolsLocation + "svnlook.exe");
			psi.RedirectStandardOutput = true;
			psi.WindowStyle = ProcessWindowStyle.Hidden;
			psi.UseShellExecute = false;
			psi.Arguments = arguments;

			Process svnLook = Process.Start(psi);
			StreamReader output = svnLook.StandardOutput;

			//NOTE: If the Standard Output for SVNLook fills up, then the process will never terminate.
			//This can occur with really large commits. As such, we need to constantly read the standard output
			//from the process until it closes.
			StringBuilder changeOutput = new StringBuilder();
			int waitCount = 0;

			//Loop until the svnLook process exits or we've waited over 30 seconds for it to finish
			while(!svnLook.HasExited) {
				if(waitCount > 30) {
					throw new Exception("SVNLook took over 30 seconds to terminate.");
				}

				//Read to the end of the standard output of the SVN Look process
				while(!output.EndOfStream) {
					changeOutput.AppendLine(output.ReadLine());
				}

				//This pause is usually long enough to get the SVN Look process to finish and exit
				Thread.Sleep(50);

				//Break early to avoid having to wait an extra 950 milliseconds to get the result if we know that the
				//svn look process has already exited.
				if(svnLook.HasExited) {
					break;
				}

				Thread.Sleep(950);
				waitCount++;
			}

			//Make sure we don't miss any output from SVNLook - it's possible the process
			//exited before we've finished reading all the output.
			while (!output.EndOfStream) {
				changeOutput.AppendLine(output.ReadLine());
			}

			return changeOutput.ToString();
		}

		public static ChangedState GetActionEnum(string action)
		{
			return (ChangedState) Enum.Parse(typeof(ChangedState), action);
		}

		//Given an enum that has a 'description' attributes, this method retrieves
		//the description attribute, or the enum name if no description attribute exists
		public static string GetEnumDescription(Enum enumName)
		{
			Type enumType = enumName.GetType();
			Type attributeType = typeof(DescriptionAttribute);

			//Find the field info for the name of the entered enum
			FieldInfo info = enumType.GetField(enumName.ToString());

			//Retrieve the list of discription attributes associated with the enum
			DescriptionAttribute[] descriptions =
				(DescriptionAttribute[])info.GetCustomAttributes(attributeType,
																  false);

			//Returns the description requested if one exists, otherwise returns the name of the enum
			return (descriptions != null && descriptions.Length > 0) ? descriptions[0].Description : enumName.ToString();
		}
	}
}
