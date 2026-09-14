using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;

namespace Diario_de_Bordo.Models
{
    public class LdapAuthService
    {
        private readonly string _ldapServer;
        private readonly int _ldapPort;
        private readonly string _searchBase;
        private readonly string _domain; // domínio AD para bind seguro

        public LdapAuthService(string ldapServer, string searchBase, string domain, int ldapPort = 389)
        {
            _ldapServer = ldapServer;
            _ldapPort = ldapPort;
            _searchBase = searchBase;
            _domain = domain; // ex: "ad"
        }

        // Autentica e retorna o nome completo do usuário
        public string? AutenticarERetornarNome(string matricula, string senha)
        {
            try
            {
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapServer, _ldapPort));
                connection.SessionOptions.ProtocolVersion = 3;

                // Bind usando domínio explicitamente
                connection.Bind(new NetworkCredential(matricula, senha, _domain));

                // Busca o nome de exibição
                var request = new SearchRequest(
                    _searchBase,
                    $"(sAMAccountName={matricula})",
                    SearchScope.Subtree,
                    "displayName"
                );

                var response = (SearchResponse)connection.SendRequest(request);

                if (response.Entries.Count == 1)
                    return response.Entries[0].Attributes["displayName"][0].ToString();

                return matricula;
            }
            catch (LdapException ex)
            {
                LogError($"LDAP Error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"General Error: {ex.Message}");
                return null;
            }
        }

        // Verifica se o usuário pertence a um grupo
        public bool UsuarioEstaNoGrupo(string matricula, string grupo, string senha)
        {
            try
            {
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapServer, _ldapPort));
                connection.SessionOptions.ProtocolVersion = 3;
                connection.Bind(new NetworkCredential(matricula, senha, _domain));

                var request = new SearchRequest(
                    _searchBase,
                    $"(&(objectClass=group)(cn={grupo})(member=CN={matricula},*))",
                    SearchScope.Subtree,
                    "cn"
                );

                var response = (SearchResponse)connection.SendRequest(request);
                return response.Entries.Count > 0;
            }
            catch (LdapException ex)
            {
                LogError($"LDAP Error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"General Error: {ex.Message}");
                return false;
            }
        }

        // Retorna todos os grupos do usuário
        public List<string>? ObterGruposDoUsuario(string matricula, string senha)
        {
            try
            {
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapServer, _ldapPort));
                connection.SessionOptions.ProtocolVersion = 3;
                connection.Bind(new NetworkCredential(matricula, senha, _domain));

                var request = new SearchRequest(
                    _searchBase,
                    $"(sAMAccountName={matricula})",
                    SearchScope.Subtree,
                    "memberOf"
                );

                var response = (SearchResponse)connection.SendRequest(request);

                if (response.Entries.Count == 1)
                {
                    var entry = response.Entries[0];
                    var atributos = entry.Attributes["memberOf"];
                    if (atributos != null)
                        return atributos.Cast<string>().ToList();
                }

                return null;
            }
            catch (LdapException ex)
            {
                LogError($"LDAP Error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"General Error: {ex.Message}");
                return null;
            }
        }

        // Método simples de log para depuração no IIS
        private void LogError(string message)
        {
            try
            {
                // Cria arquivo de log no IIS (pode ajustar caminho se precisar)
                File.AppendAllText(@"C:\inetpub\wwwroot\ldap.log", $"{DateTime.Now}: {message}\n");
            }
            catch
            {
                // Ignora falha de log para não quebrar autenticação
            }
        }

        public List<string> ObterTodosGruposDoUsuario(string usuario)
        {
            try
            {
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(_ldapServer, _ldapPort));
                connection.SessionOptions.ProtocolVersion = 3;

                // Bind com usuário de serviço que tem permissão de leitura de todos os grupos
                connection.Bind(new NetworkCredential("Administrator", "@N3x1m#", _domain));

                // 1️⃣ Obter DN completo do usuário
                var userRequest = new SearchRequest(
                    _searchBase,
                    $"(sAMAccountName={usuario})",
                    SearchScope.Subtree,
                    "distinguishedName"
                );

                var userResponse = (SearchResponse)connection.SendRequest(userRequest);

                if (userResponse.Entries.Count != 1)
                {
                    LogError($"Usuário {usuario} não encontrado no AD.");
                    return new List<string>();
                }

                var userDN = userResponse.Entries[0].Attributes["distinguishedName"][0].ToString();

                // 2️⃣ Buscar grupos onde o usuário é membro
                var groupRequest = new SearchRequest(
                    _searchBase,
                    $"(&(objectClass=group)(member={userDN}))",
                    SearchScope.Subtree,
                    "cn"
                );

                var groupResponse = (SearchResponse)connection.SendRequest(groupRequest);

                var grupos = groupResponse.Entries
                    .Cast<SearchResultEntry>()
                    .Select(e => e.Attributes["cn"][0].ToString())
                    .ToList();

                // Log para depuração no IIS
                System.IO.File.AppendAllText(@"C:\inetpub\wwwroot\ldap_debug.log",
                    $"{DateTime.Now}: Grupos encontrados para {usuario}: {string.Join(", ", grupos)}\n");

                return grupos;
            }
            catch (Exception ex)
            {
                LogError($"ERRO ao obter grupos de {usuario}: {ex.Message}");
                return new List<string>();
            }
        }



    }
}
