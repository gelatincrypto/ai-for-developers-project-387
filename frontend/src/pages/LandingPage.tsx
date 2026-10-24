import { Button, Container, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router'

const steps = [
  { title: 'Выберите время', description: 'Найдите удобное время для звонка.' },
  { title: 'Забронируйте', description: 'Зарезервируйте выбранное время.' },
  { title: 'Созвонитесь', description: 'Встретьтесь в запланированное время.' },
]

function LandingPage() {
  return (
    <Container component="main" size="md" py="xl">
      <Stack gap="xl">
        <Stack gap="md" align="flex-start" py="xl">
          <Title order={1}>Календарь звонков</Title>
          <Text size="lg" c="dimmed">
            Сервис поможет выбрать удобное время в календаре владельца и забронировать звонок онлайн.
          </Text>
          <Button component={Link} to="/booking" size="lg">
            Перейти к бронированию
          </Button>
          <Button component={Link} to="/owner" variant="subtle">Вход для владельца</Button>
        </Stack>
        <Stack gap="md">
          <Title order={2}>Как это будет работать</Title>
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            {steps.map((step) => (
              <Paper key={step.title} withBorder radius="md" p="lg">
                <Stack gap="xs">
                  <Title order={3} size="h4">{step.title}</Title>
                  <Text c="dimmed">{step.description}</Text>
                </Stack>
              </Paper>
            ))}
          </SimpleGrid>
        </Stack>
      </Stack>
    </Container>
  )
}

export default LandingPage
