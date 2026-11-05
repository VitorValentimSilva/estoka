// Utilidades de interface do Estoka (sem dependências).
window.estoka = {
  /** Salva a preferência de tema em cookie (lido no servidor para evitar "piscar" no carregamento). */
  setTheme(theme) {
    document.cookie = `estoka-theme=${theme}; path=/; max-age=31536000; samesite=lax`;
    document.body.classList.toggle('estoka-dark', theme === 'dark');
    document.body.classList.toggle('estoka-light', theme !== 'dark');
  },
  /** Faz download de um arquivo a partir de bytes (base64). */
  download(fileName, contentType, base64) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
  },
  print() { window.print(); }
};

// Login: botões "Acesso de demonstração" preenchem e-mail e senha.
document.addEventListener('click', (event) => {
  const button = event.target.closest('[data-demo-email]');
  if (!button) return;
  const email = document.getElementById('email');
  const password = document.getElementById('password');
  if (email && password) {
    email.value = button.dataset.demoEmail;
    password.value = button.dataset.demoPassword;
    document.querySelectorAll('.demo-access__user').forEach(b => b.classList.toggle('is-selected', b === button));
    password.focus();
  }
});
