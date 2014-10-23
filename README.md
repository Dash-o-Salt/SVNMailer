SVN Mailer Documentation and FAQ
=========

Formats and sends SVN revision emails based on the format of the old CVS Mailer.

Quick start:

1. Edit the app.config for configuration. SVNLook.exe is required for execution of this tool, a utility that is distributed with the standard SVN package. Note that for this utility to execute it needs direct access to the repository (see FAQ below). As such, it should be executing on the SVN server, which should already have SVN (and therefore svnlook) installed.
2. Set your email server and email address.
3. Set the URLs for web based viewing of your repository. Technically these aren't really necessary to be able to send the commit email, but you might have to edit the source code to remove dependencies on these links.
4. Add the SVN mailer to your svn post-commit hook, usually located at a location similar to this: C:\Repositories\{Your Repository}\hooks\post-commit.cmd

FAQ:

1. Why a custom mailer?

The existing SVN Mailers will certainly suffice for sending out basic commit emails, but none of them has the necessary custom configuration we wanted. That is to say, none of them allowed us to format the commit email to our satisfaction. Our ultimate goal was to have a mailer that produces emails formatted similarly to how the old CVS mailer formatted them. 

2. SVN Look

To accomplish this goal, a SVN utility called 'SVNLook' was used to peek at particular revisions inside the source control, obtaining the necessary information for generating a commit email. In particular, two SVN Look commands were used:

* info (retrieves the author, changed date, and log of the revision)
* changed (retrieves the files changed during the revision, including whether those files were added, removed, or updated) 

3. SVN Post-Commit Hooks

Whenever a commit is received on the SVN server, the file C:\Repositories\Repo\hooks\post-commit.cmd is called (the file location may vary on your SVN server). Everything in that batch file is run before the commit is complete. This is run synchronously with the Tortoise SVN UI, such that if an error occurs during a post-commit step the exception will be written to the client's screen.

The previous mailer setup in the post commit script for SVN could look like this:

c:\Python26\python.exe c:\Python26\Scripts\svn-mailer --repository=%1 --revision=%2 --config="C:\Program Files (x86)\VisualSVN Server\svn-mailer\mailer.conf"

The new mailer uses this line instead:

C:\YourMailerLocation\SVNMailer.exe -repository=%1 -revision=%2

In either case, the repository variable passed in by SVN will be 'C:\Repositories\Repo' (your repo location will vary) and the revision variable will be the revision of the check-in.

4. Debugging the Mailer before deployment

To debug the mailer locally, several steps need to be taken:

1. Edit the app.config. Change the 'SendEmailAddress' key to point to your own email address such that any test runs you do will be sent directly to you.
2. Change the 'SVNLookLocation' key to point to the folder where the SVNLook.exe application lives.
3. (Optional) Change the subject line of the sent email to a temporary subject in the source code, such that any mailer emails won't be automatically filtered into your SVN check-in folder if you already have rules setup for commit emails (this aids in determining the difference between debug emails and actual check-in emails). The subject line set can be located in the 'SendMail' function.
4. Make sure you have access to the repository through a UNC path or you are running the application on your SVN server - SVNLook does not work off of URLs.
5. Right click on the SVNEEDMailer project in the solution and select 'properties.' Select the 'debug' tab, and under the 'Start Options' section, input the below into the 'Command line arguments' section.
	  * -repository={Repository Location} -revision={Your Revision Number Here} 
6. Right click on the project in the solution explorer and select 'Debug -> Start New Instance.' This will execute the mailer. If you need to break into a certain point, set your breakpoints before starting a new instance. 

The final result should be an email sent to your email address in the current email format the mailer has. Tweaks can then be made to the mailer. Bear in mind that once you are done, you must reset the 'SendEmailAddress', 'SVNLookLocation', and  subject line of the email back to their default state before deploying the application to the production environment.
