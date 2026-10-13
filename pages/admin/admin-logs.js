// כתובת שרת ה-API של המערכת
const API_URL = 'http://localhost:5057';

// איך שמתסיים להטען הדף בדפדפן
window.onload = function()
{
    // חיבור כפתור ההתנתקות של המנהל
    const logoutBtn = document.getElementById('adminLogoutBtn');
    if (logoutBtn) 
    {
        logoutBtn.onclick = logoutAdmin; // רושמים בלי סוגריים כדי שהפונקציה תרוץ רק בלחיצה ולא מיד כשהדף עולה
    }

    // חיבור כפתור ה-Refresh לרענון ידני של היומן
    const refreshBtn = document.getElementById('refreshBtn');
    if (refreshBtn)
    {
        refreshBtn.onclick = function()
        {
            loadLogs(); // קריאה לפונקציית טעינת היומן
        };
    }

    // חיבור שורת החיפוש לסינון מהיר בזמן אמת
    const searchInput = document.getElementById('searchInput');
    if (searchInput)
    {
        searchInput.oninput = function()
        {
            searchLogs(); // קריאה לפונקציית הסינון
        };
    }

    // קריאה ראשונית לטעינת כל הפעולות מהשרת מיד בפתיחת הדף
    loadLogs();
};

// פונקציה לשליפת הפעולות מהשרת והצגתן בטבלה
async function loadLogs() 
{
    // שליפת הטוקן של המנהל מהזיכרון של הדפדפן
    const authToken = localStorage.getItem('authToken');

    // שולחים בקשה לשרת יחד עם הטוקן ומקבלים את הרשימה מהמסד
    const response = await fetch(API_URL + '/api/admin/logs?token=' + authToken);
    const logs = await response.json();

    // משתנים לשורות הטבלה ולספירת הפעולות
    let rowsHtml = '';
    let totalCount = logs.length;
    let editCount = 0;
    let deleteCount = 0;
    let loginCount = 0;

    // לולאה שעוברת שורה שורה
    for (let i = 0; i < logs.length; i++) 
    {
        const log = logs[i];

        // בדיקה איזה עיצוב תגית ואייקון לשים לפי סוג הפעולה
        let badgeClass = 'badge-action-edit';
        let iconClass = 'fa-solid fa-pen-to-square';

        if (log.action_type === 'מחיקת משתמש') 
        {
            badgeClass = 'badge-action-delete';
            iconClass = 'fa-solid fa-trash-can';
            deleteCount = deleteCount + 1;
        } 
        else if (log.action_type === 'התחברות מנהל') 
        {
            badgeClass = 'badge-action-login';
            iconClass = 'fa-solid fa-arrow-right-from-bracket';
            loginCount = loginCount + 1;
        } 
        else 
        {
            // ברירת מחדל עריכת משתמש
            editCount = editCount + 1;
        }

        // בניית שורת הטבלה  
        rowsHtml = rowsHtml + '<tr>' +
            '<td>' + log.id + '</td>' +
            '<td>' +
                '<span class="log-badge ' + badgeClass + '">' +
                    '<i class="' + iconClass + '"></i> ' +
                    log.action_type +
                '</span>' +
            '</td>' +
            '<td class="log-details-text">' + log.details + '</td>' +
            '<td><span class="log-time-text">' + log.created_at + '</span></td>' +
        '</tr>';
    }

    // אם אין עדיין פעולות כלל ביומן
    if (logs.length === 0) 
    {
        rowsHtml = '<tr><td colspan="4" style="text-align:center; padding:24px; color:#64748b;">אין עדיין פעולות שנרשמו ביומן</td></tr>';    }

    // הזרקת השורות לתוך הטבלה בדף
    document.getElementById('logsTableBody').innerHTML = rowsHtml;

    // עדכון המספרים בקוביות הסיכום למעלה
    document.getElementById('totalLogsCount').textContent = totalCount;
    document.getElementById('editLogsCount').textContent = editCount;
    document.getElementById('deleteLogsCount').textContent = deleteCount;
    document.getElementById('loginLogsCount').textContent = loginCount;
    document.getElementById('tableCountText').textContent = 'מציג ' + totalCount + ' פעולות אחרונות';
}

// חיפוש בטבלה
function searchLogs() 
{
    // מה שהמשתמש הקליד
    const searchText = document.getElementById('searchInput').value.toLowerCase();
    
    // גוף הטבלה והשורות שבתוכו
    const tableBody = document.getElementById('logsTableBody');
    const rows = tableBody.rows;
    let visibleCount = 0; //  משתנה לכמה שורות נשארו גלויות

    // לולאה שעוברת שורה שורה
    for (let i = 0; i < rows.length; i++) 
    {
        const rowText = rows[i].textContent.toLowerCase();

        // אם המילה קיימת בשורה מציגים, אם לא מסתירים
        if (rowText.includes(searchText) === true) 
        {
            rows[i].style.display = ''; // מציג
            visibleCount++; // מוסיפים 1 לספירה של מי שרואים    
        } 
        else 
        {   
            rows[i].style.display = 'none'; // מסתיר
        }

        // עדכון המספר בטבלה בזמן אמת
        document.getElementById('tableCountText').textContent = 'מציג ' + visibleCount + ' פעולות';
    }
}

 // פונקציית התנתקות
async function logoutAdmin() 
{
    const confirmResult = await Swal.fire({
        title: 'התנתקות מהמערכת',
        text: 'לצורך שמירה על אבטחת המידע, האם אתה בטוח שברצונך לצאת מחשבונך ולבצע התנתקות מהחשבון?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#0a4ade',
        cancelButtonColor: '#94a3b8',
        confirmButtonText: 'אישור ויציאה',
        cancelButtonText: 'ביטול'
    });

    if (confirmResult.isConfirmed === true) 
    {
        const authToken = localStorage.getItem('authToken');
        if (authToken) 
        {
            await fetch(API_URL + '/api/logout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: authToken })
            });
        }
        localStorage.clear(); // מוחק את הלוקל סטורג 
        window.location.replace('../login.html'); // מעביר ישר ללוגין והנקודה בהתחלה אומר לו לצאת מהתקייה
    }
}