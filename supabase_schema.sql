-- ============================================================
-- ChessMAUI — Schema Supabase
-- Execute no SQL Editor do painel: supabase.com → SQL Editor
-- ============================================================

-- ── Tabela: profiles ─────────────────────────────────────────
create table if not exists public.profiles (
  id              uuid        primary key references auth.users on delete cascade,
  name            text        not null default '',
  elo             int         not null default 1200,
  week_elo        int         not null default 0,
  wins            int         not null default 0,
  losses          int         not null default 0,
  tournaments_won int         not null default 0,
  avatar          text        not null default '♟',
  avatar_path     text,
  country         text,
  state_abbr      text,
  updated_at      timestamptz not null default now(),
  is_admin        boolean     not null default false
);

alter table public.profiles enable row level security;

-- Protege is_admin de ser alterado pelo próprio app: o cliente pode mandar qualquer valor
-- nesse campo no upsert do perfil (ver ProfileService.SyncToSupabaseAsync), mas esse gatilho
-- sempre restaura o valor antigo quando quem está atualizando é o app (papel authenticated/
-- anon via PostgREST) — só uma atualização feita direto pelo SQL Editor do painel (rodando
-- como superusuário, sem esse papel) consegue realmente mudar esse campo.
create or replace function public.protect_is_admin()
returns trigger language plpgsql as $$
begin
  if auth.role() in ('authenticated', 'anon') then
    new.is_admin := old.is_admin;
  end if;
  return new;
end;
$$;

drop trigger if exists protect_is_admin_trigger on public.profiles;
create trigger protect_is_admin_trigger
  before update on public.profiles
  for each row execute procedure public.protect_is_admin();

-- Qualquer usuário autenticado pode ler perfis (ranking público)
create policy "Perfis são públicos para leitura"
  on public.profiles for select
  using (true);

-- Usuário só edita o próprio perfil
create policy "Usuário edita o próprio perfil"
  on public.profiles for insert
  with check (auth.uid() = id);

create policy "Usuário atualiza o próprio perfil"
  on public.profiles for update
  using (auth.uid() = id);

-- Cria linha em profiles automaticamente ao cadastrar
create or replace function public.handle_new_user()
returns trigger language plpgsql security definer as $$
begin
  insert into public.profiles (id, name)
  values (
    new.id,
    coalesce(new.raw_user_meta_data->>'username', '')
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute procedure public.handle_new_user();


-- ── Tabela: challenges (desafios de amigo) ───────────────────
create table if not exists public.challenges (
  id              uuid        primary key default gen_random_uuid(),
  code            text        not null unique,
  challenger_id   uuid        references auth.users,
  challenger_name text        not null,
  time_minutes    int         not null default 0,
  status          text        not null default 'pending',  -- 'pending' | 'accepted' | 'expired'
  created_at      timestamptz not null default now(),
  expires_at      timestamptz not null
);

create index if not exists challenges_code_idx    on public.challenges (code);
create index if not exists challenges_expires_idx on public.challenges (expires_at);

alter table public.challenges enable row level security;

-- Qualquer usuário pode ler desafios pendentes pelo código
create policy "Desafios pendentes são visíveis"
  on public.challenges for select
  using (status = 'pending' and expires_at > now());

-- Usuários autenticados (inclusive anônimos) criam desafios — mas só atribuindo a si mesmo
-- como desafiante (antes dava pra criar um desafio em nome de qualquer challenger_id).
create policy "Usuário cria desafio"
  on public.challenges for insert
  with check (auth.role() = 'authenticated' and challenger_id = auth.uid());

-- Qualquer usuário autenticado pode aceitar um desafio pendente — mas só pode levar o status
-- de 'pending' para 'accepted', nunca mexer num desafio já aceito/expirado nem setar outro
-- status qualquer (antes dava pra atualizar QUALQUER linha, de qualquer jeito).
create policy "Usuário aceita desafio pendente"
  on public.challenges for update
  using (auth.role() = 'authenticated' and status = 'pending' and expires_at > now())
  with check (status = 'accepted');


-- ── Limpeza periódica de desafios expirados ──────────────────
-- Opcional: cron via pg_cron (extensão do Supabase)
-- select cron.schedule('limpar-desafios', '*/15 * * * *',
--   $$delete from public.challenges where expires_at < now()$$);


-- ── Storage: fotos de avatar ──────────────────────────────────
-- O SQL Editor não cria buckets — faça uma vez, manualmente, no painel:
--   Storage → New bucket → nome exatamente "avatars" → marcar "Public bucket" → Save.
-- Depois de criar o bucket, rode o bloco abaixo aqui no SQL Editor pra liberar upload:

-- Qualquer usuário autenticado pode subir/atualizar SÓ o próprio arquivo (nome do arquivo
-- é sempre "<user_id>.jpg", ver ProfileService.UploadAvatarIfLocalAsync).
create policy "Usuário sobe a própria foto de avatar"
  on storage.objects for insert
  with check (
    bucket_id = 'avatars'
    and auth.role() = 'authenticated'
    and name = auth.uid()::text || '.jpg'
  );

create policy "Usuário substitui a própria foto de avatar"
  on storage.objects for update
  using (
    bucket_id = 'avatars'
    and auth.role() = 'authenticated'
    and name = auth.uid()::text || '.jpg'
  );

-- Leitura pública (o bucket "Public" já libera isso sozinho, esta política é redundante
-- mas explícita — sem ela, se o bucket virar privado um dia, as fotos somem de todo mundo).
create policy "Fotos de avatar são públicas para leitura"
  on storage.objects for select
  using (bucket_id = 'avatars');
