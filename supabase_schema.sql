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
  updated_at      timestamptz not null default now()
);

alter table public.profiles enable row level security;

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

-- Usuários autenticados (inclusive anônimos) criam desafios
create policy "Usuário cria desafio"
  on public.challenges for insert
  with check (auth.role() = 'authenticated');

-- Qualquer usuário autenticado pode aceitar (atualizar status)
create policy "Usuário aceita desafio"
  on public.challenges for update
  using (auth.role() = 'authenticated');


-- ── Limpeza periódica de desafios expirados ──────────────────
-- Opcional: cron via pg_cron (extensão do Supabase)
-- select cron.schedule('limpar-desafios', '*/15 * * * *',
--   $$delete from public.challenges where expires_at < now()$$);
