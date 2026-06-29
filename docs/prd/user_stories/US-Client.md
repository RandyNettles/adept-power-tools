# EPIC: Adept Power Tools – Client Connection & Navigation

Help document administrators easily connect to the system, understand their connection status, and confidently access the right tools once they are signed in.

---

## US-CONN-01 Guided Access to Features

**User Story**  
As a document administrator, I want the system to guide me to connect before accessing other features  
so that I don’t accidentally try to perform actions without being signed in.

**Acceptance Criteria**
- The application opens on the Connect screen
- Workflow and Import features are unavailable until I am connected
- Once I connect successfully, those features become available immediately
- The system clearly reflects whether I am connected or not

---

## US-CONN-02 Simple and Clear Connection Options

**User Story**  
As a document administrator, I want to easily choose how I connect  
so that I can use the correct method for my environment.

**Acceptance Criteria**
- I can choose how to connect (e.g., standard server connection or other supported modes)
- The screen updates to show only the fields I need based on my selection
- Required fields are clearly shown for each connection type
- The interface helps prevent entering incorrect or unnecessary information

---

## US-CONN-03 Clear Connection Status and Feedback

**User Story**  
As a document administrator, I want to clearly see whether I am connected or if something went wrong  
so that I know what I can do next.

**Acceptance Criteria**
- The system shows clear connection states (Disconnected, Connecting, Connected, Error)
- Status messages update in real time during connection
- If a connection fails, I see a clear explanation of the issue
- I can easily understand whether I need to retry or fix something

---

## US-CONN-04 Choose the Correct Account

**User Story**  
As a document administrator with access to multiple accounts, I want to choose the account I need  
so that I can work in the correct environment.

**Acceptance Criteria**
- If multiple accounts are available, I am prompted to choose one
- I can select the correct account without restarting the process
- Once selected, I am connected using that account
- If I cancel, I am returned to a disconnected state

---

## US-CONN-05 Faster Reconnection with Saved Details

**User Story**  
As a document administrator, I want the system to remember my previous connection details  
so that I can reconnect quickly.

**Acceptance Criteria**
- The system remembers previously used server addresses
- My last used username is pre-filled when I return
- Saved connections are available when I open the application
- I can select a previous entry instead of retyping it

---

## US-CONN-06 Manage Connection Profiles (COM Users)

**User Story**  
As a document administrator using saved connection profiles, I want to manage those profiles  
so that I can switch between different systems without re-entering details.

**Acceptance Criteria**
- I can create a new saved profile
- I can update an existing profile
- I can remove a profile I no longer need
- My profiles are saved and available when I restart the application
- I can choose a profile when connecting

---
