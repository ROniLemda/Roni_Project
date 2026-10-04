// כתובת השרת
const API_URL = 'http://localhost:5057';

window.onload = async function() 
{
   // בדיקה אם יש משתמש שמור בזיכרון של הדפדפן
    const savedUser = localStorage.getItem('currentUser');

    // אם לא מחובר זורק אותו להתחברות
    if (savedUser === null) 
    {
        window.location.href = 'login.html';
        return;
    }

    // שולפים את הטוקן מזיכרון הדפדפן
    const authToken = localStorage.getItem('authToken');

    // בדיקה אם אין טוקן מעיפים מיד להתחברות
    if (authToken === null || authToken === '') 
    {
        window.location.replace('login.html');
        return;
    }

    // אבטחה מול השרת בודקים שהטוקן באמת קיים במסד הנתונים ולא זויף בקונסול
    const response = await fetch(API_URL + '/api/verify-token', 
    {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: authToken })
    });

    const result = await response.text();
    // אם השרת לא החזיר שהטוקן תקין (אז האקר / טוקן פג תוקף) מנקים זיכרון ומעיפים
    if (result.includes('אינו תקין') === true)
    {
        localStorage.removeItem('authToken');
        localStorage.removeItem('currentUser');
        window.location.replace('login.html');
        return;
    }

    //  אם הגענו לפה אז תקין השרת מחזיר: את התשובה (תקין) | שם |מייל מפרקים ומציגים לפי הסדר
    const parts = result.split('|');
    const fullName = parts[1];
    const userEmail = parts[2];

    // מעדכן את השם משתמש והשם פרטי בדשבורד 
    document.getElementById('userNameDisplay').textContent = fullName;
    document.getElementById('userEmailDisplay').textContent = userEmail;

    const logoutBtn = document.getElementById('logoutBtn'); // כפתור ההתנתקות 
    logoutBtn.onclick = async function() 
    {
        const confirmResult = await Swal.fire({
            title:'התנתקות מהמערכת',
            text: 'לצורך שמירה על אבטחת המידע, האם אתה בטוח שברצונך לצאת מחשבונך ולבצע התנתקות מהחשבון?',
            icon:'question',
            showCancelButton: true,
            confirmButtonColor: '#0a4ade',
            cancelButtonColor: '#94a3b8',
            confirmButtonText: 'אישור ויציאה',
            cancelButtonText: 'ביטול'
        });

        // אם המשתמש לחץ על כפתור אישור ויציאה
        if (confirmResult.isConfirmed === true) 
        {
            // שליחת בקשה לשרת למחיקת הטוקן ממסד הנתונים
            await fetch(API_URL + '/api/logout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: authToken })
            });

            // מנקים את כל הנתונים של המשתמש מהדפדפן
            localStorage.removeItem('authToken');
            localStorage.removeItem('currentUser');
            // מעבירים חזרה למסך ההתחברות
            window.location.replace('login.html');
        }
    };
};
