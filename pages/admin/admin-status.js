// כתובת שרת ה-API של המערכת
const API_URL = 'http://localhost:5057';

// בעת טעינת הדף
window.onload = async function() 
{
    // בדיקת אבטחה מול השרת לוודא שהטוקן בתוקף
    const isAuth = await checkAdminAuth();
    if (isAuth === false) 
    {
        return;
    }

    // בדיקה האם המשתמש הוא מנהל מורשה מול השרת
    const authToken = localStorage.getItem('authToken');
    const adminCheck = await fetch(API_URL + '/api/admin/logs?token=' + authToken);
    if (adminCheck.status === 401)
    {
        window.location.replace('../dashboard.html');
        return;
    }

    // חיבור כפתור ההתנתקות של המנהל
    const logoutBtn = document.getElementById('adminLogoutBtn');
    if (logoutBtn) 
    {
        logoutBtn.onclick = logoutAdmin;
    }
};

// פונקציה לבדיקת תקינות הטוקן של המנהל מול השרת
async function checkAdminAuth() 
{
    const authToken = localStorage.getItem('authToken');
    if (authToken === null || authToken === '') 
    {
        localStorage.clear();
        window.location.replace('../login.html');
        return false;
    }

    // בדיקת תקינות הטוקן מול השרת
    const response = await fetch(API_URL + '/api/verify-token', 
    {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: authToken })
    });

    const result = await response.text();
    // אם הטוקן פג תוקף או אינו תקין מנקים את הזיכרון ומעיפים להתחברות
    if (result.includes('אינו תקין') === true) 
    {
        localStorage.clear();
        window.location.replace('../login.html');
        return false;
    }

    return true;
}

// פונקציית התנתקות מנהל
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
        localStorage.clear();
        window.location.replace('../login.html');
    }
}
